using Jellyfin.Plugin.UserAnalytics.Data;
using Jellyfin.Plugin.UserAnalytics.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.UserAnalytics;

/// <summary>
/// Registers the plugin's services into the host DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IPlaybackRepository, SqlitePlaybackRepository>();
        serviceCollection.AddSingleton<LogImportService>();
        serviceCollection.AddHostedService<PlaybackTrackingService>();
    }
}
