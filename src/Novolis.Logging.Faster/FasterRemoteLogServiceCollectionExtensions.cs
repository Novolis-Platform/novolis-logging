using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Novolis.Logging.Core;

namespace Novolis.Logging.Faster;

public static class FasterRemoteLogServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="FasterRemoteLogStore"/>. When a <see cref="RemoteLogHub"/> is
    /// also registered and <see cref="FasterRemoteLogOptions.AttachToHub"/> is true, the store
    /// attaches on construction.
    /// </summary>
    public static IServiceCollection AddFasterRemoteLog(
        this IServiceCollection services,
        Action<FasterRemoteLogOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<FasterRemoteLogOptions>();

        services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<FasterRemoteLogOptions>>();
            var store = new FasterRemoteLogStore(options);
            if (options.Value.AttachToHub)
            {
                var hub = sp.GetService<RemoteLogHub>();
                if (hub is not null)
                    store.Attach(hub);
            }

            return store;
        });

        return services;
    }
}
