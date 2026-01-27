using Jellyfin.Plugin.WatchSync.Services;
using Jellyfin.Plugin.WatchSync.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.WatchSync;

/// <summary>
/// Registers plugin services in the dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IMediaMatcher, MediaMatcher>();
        serviceCollection.AddSingleton<IConflictResolver, ConflictResolver>();
        serviceCollection.AddHostedService<WatchSyncService>();
        serviceCollection.AddSingleton<IScheduledTask, FullSyncTask>();
    }
}
