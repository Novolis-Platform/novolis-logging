using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Novolis.Logging.Contracts;
using Novolis.Logging.Core;
using Novolis.Logging.Faster;

namespace Novolis.Logging.Unit;

public sealed class FasterRemoteLogStoreTests
{
    [Test]
    public async Task Append_Commit_Survives_Reopen()
    {
        var dir = CreateTempDir();
        try
        {
            await using (var store = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = false,
            }))
            {
                store.Append(new RemoteLogLine
                {
                    T = 42,
                    L = (int)LogLevel.Information,
                    C = "test",
                    M = "durable",
                    SessionId = "s1",
                });
                await store.CommitAsync();
            }

            await using var reopened = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = false,
            });
            var lines = await reopened.ReadCommittedAsync();
            await Assert.That(lines.Count).IsEqualTo(1);
            await Assert.That(lines[0].M).IsEqualTo("durable");
            await Assert.That(lines[0].SessionId).IsEqualTo("s1");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Test]
    public async Task Hub_Attach_Persists_Appended_Lines()
    {
        var dir = CreateTempDir();
        try
        {
            var hub = new RemoteLogHub();
            await using var store = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = false,
            });
            store.Attach(hub);

            hub.Append(new RemoteLogLine { M = "from-hub", L = (int)LogLevel.Warning, C = "h" });
            await store.CommitAsync();

            var lines = await store.ReadCommittedAsync();
            await Assert.That(lines.Select(l => l.M).ToArray()).IsEquivalentTo(["from-hub"]);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Test]
    public async Task AddFasterRemoteLog_Attaches_To_Hub()
    {
        var dir = CreateTempDir();
        try
        {
            var services = new ServiceCollection();
            services.AddRemoteLogging();
            services.AddFasterRemoteLog(o =>
            {
                o.Directory = dir;
                o.AutoCommit = false;
            });

            await using var sp = services.BuildServiceProvider();
            var hub = sp.GetRequiredService<RemoteLogHub>();
            var store = sp.GetRequiredService<FasterRemoteLogStore>();

            hub.Append(new RemoteLogLine { M = "di", L = (int)LogLevel.Information, C = "x" });
            await store.CommitAsync();

            var lines = await store.ReadCommittedAsync(maxCount: 5);
            await Assert.That(lines[^1].M).IsEqualTo("di");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Test]
    public async Task AutoCommit_Dispose_Detach_And_SyncCommit()
    {
        var dir = CreateTempDir();
        try
        {
            var hub = new RemoteLogHub();
            using (var store = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = true,
                CommitInterval = TimeSpan.FromMilliseconds(20),
            }))
            {
                store.Attach(hub);
                store.Attach(hub); // same hub early-out
                hub.Append(new RemoteLogLine { M = "auto", L = (int)LogLevel.Information });
                await Task.Delay(80);
                store.Commit(spinWait: true);
                store.Detach();
                store.Append(new RemoteLogLine { M = "direct", L = (int)LogLevel.Warning });
                store.Commit(spinWait: true);
            }

            await using var reopened = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = false,
            });
            var emptyMax = await reopened.ReadCommittedAsync(maxCount: 0);
            await Assert.That(emptyMax.Count).IsEqualTo(0);
            var lines = await reopened.ReadCommittedAsync();
            await Assert.That(lines.Count).IsGreaterThanOrEqualTo(1);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Test]
    public async Task Ctor_MissingDirectory_Throws()
    {
        await Assert.That(() => new FasterRemoteLogStore(new FasterRemoteLogOptions { Directory = " " }))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Ctor_IOptionsNull_And_ZeroCommitInterval()
    {
        await Assert.That(() => new FasterRemoteLogStore((IOptions<FasterRemoteLogOptions>)null!))
            .Throws<ArgumentNullException>();

        var dir = CreateTempDir();
        try
        {
            await using var store = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = false,
                CommitInterval = TimeSpan.Zero,
            });
            await Assert.That(store.CommitInterval).IsEqualTo(TimeSpan.FromMilliseconds(50));
            await Assert.That(store.CommittedUntilAddress).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Test]
    public async Task ReadCommitted_Empty_DisposeAsync_And_IdempotentDispose()
    {
        var dir = CreateTempDir();
        try
        {
            var store = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = true,
                CommitInterval = TimeSpan.FromMilliseconds(30),
            });
            var empty = await store.ReadCommittedAsync();
            await Assert.That(empty.Count).IsEqualTo(0);
            await store.DisposeAsync();
            await store.DisposeAsync();

            var sync = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = false,
            });
            sync.Dispose();
            sync.Dispose();
            await Assert.That(() => _ = sync.CommittedUntilAddress).Throws<ObjectDisposedException>();
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Test]
    public async Task OnLineAppended_AfterDispose_DoesNotThrowFromHub()
    {
        var dir = CreateTempDir();
        try
        {
            var hub = new RemoteLogHub();
            var store = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = false,
            });
            store.Attach(hub);
            store.Dispose();
            hub.Append(new RemoteLogLine { M = "after-dispose", L = (int)LogLevel.Information });
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Test]
    public async Task Attach_NullHub_Throws()
    {
        var dir = CreateTempDir();
        try
        {
            await using var store = new FasterRemoteLogStore(new FasterRemoteLogOptions
            {
                Directory = dir,
                AutoCommit = false,
            });
            await Assert.That(() => store.Attach(null!)).Throws<ArgumentNullException>();
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Test]
    public async Task AddFasterRemoteLog_NullConfigure_And_MaxCount_Trim()
    {
        var dir = CreateTempDir();
        try
        {
            var services = new ServiceCollection();
            services.AddRemoteLogging();
            services.Configure<FasterRemoteLogOptions>(o =>
            {
                o.Directory = dir;
                o.AutoCommit = false;
                o.PageSizeBits = 14;
                o.MemorySizeBits = 22;
                o.SegmentSizeBits = 20;
            });
            services.AddFasterRemoteLog();

            await using var sp = services.BuildServiceProvider();
            var store = sp.GetRequiredService<FasterRemoteLogStore>();
            for (var i = 0; i < 5; i++)
                store.Append(new RemoteLogLine { M = $"m{i}", L = (int)LogLevel.Information });
            await store.CommitAsync();
            var trimmed = await store.ReadCommittedAsync(maxCount: 2);
            await Assert.That(trimmed.Count).IsEqualTo(2);
            await Assert.That(trimmed[^1].M).IsEqualTo("m4");
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "novolis-logging-faster-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Temp cleanup best-effort on Windows file locks.
        }
    }
}
