using System.Text;
using Novolis.Logging.Contracts;

namespace Novolis.Logging.Unit;

[NotInParallel]
public sealed class RemoteLogJsonTests
{
    [Test]
    public async Task SerializeLine_AppendsUtf8Newline()
    {
        var line = new RemoteLogLine { T = 1, L = 2, C = "cat", M = "hello" };
        var bytes = RemoteLogJson.SerializeLine(line);
        await Assert.That(Encoding.UTF8.GetString(bytes)).EndsWith("\n");
        await Assert.That(bytes[^1]).IsEqualTo((byte)'\n');
    }

    [Test]
    public async Task TryDeserializeLine_RoundTripsFields()
    {
        var original = new RemoteLogLine
        {
            T = 42,
            L = 3,
            C = "scope",
            M = "msg",
            X = "ex",
            SessionId = "s1",
            Scope = new Dictionary<string, string> { ["k"] = "v" }
        };
        var json = Encoding.UTF8.GetString(RemoteLogJson.SerializeLine(original)).TrimEnd('\n');
        var parsed = RemoteLogJson.TryDeserializeLine(json);
        await Assert.That(parsed).IsNotNull();
        await Assert.That(parsed!.T).IsEqualTo(42);
        await Assert.That(parsed.L).IsEqualTo(3);
        await Assert.That(parsed.C).IsEqualTo("scope");
        await Assert.That(parsed.M).IsEqualTo("msg");
        await Assert.That(parsed.X).IsEqualTo("ex");
        await Assert.That(parsed.SessionId).IsEqualTo("s1");
        await Assert.That(parsed.Scope!["k"]).IsEqualTo("v");
    }

    [Test]
    public async Task TryDeserializeLine_Blank_ReturnsNull()
    {
        await Assert.That(RemoteLogJson.TryDeserializeLine("")).IsNull();
        await Assert.That(RemoteLogJson.TryDeserializeLine("   ")).IsNull();
    }

    [Test]
    public async Task TryDeserializeLine_InvalidJson_ReturnsNull()
    {
        await Assert.That(RemoteLogJson.TryDeserializeLine("{not-json")).IsNull();
    }

    [Test]
    public async Task Configure_Null_Throws()
    {
        await Assert.That(() => RemoteLogJson.Configure(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Configure_CustomOptions_RoundTrips()
    {
        var previous = RemoteLogJson.CreateDefaultOptions();
        var options = RemoteLogJson.CreateDefaultOptions();
        RemoteLogJson.Configure(options);
        try
        {
            var line = new RemoteLogLine { M = "cfg", L = 1 };
            var json = Encoding.UTF8.GetString(RemoteLogJson.SerializeLine(line)).TrimEnd('\n');
            var parsed = RemoteLogJson.TryDeserializeLine(json);
            await Assert.That(parsed!.M).IsEqualTo("cfg");
        }
        finally
        {
            RemoteLogJson.Configure(previous);
        }
    }

    [Test]
    public async Task CreateDefaultOptions_IgnoresNullProperties()
    {
        var options = RemoteLogJson.CreateDefaultOptions();
        await Assert.That(options.DefaultIgnoreCondition)
            .IsEqualTo(System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
    }
}
