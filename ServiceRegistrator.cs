using JellyfinStreamingPlugin.Api;
using JellyfinStreamingPlugin.Channels;
using JellyfinStreamingPlugin.Services;
using JellyfinStreamingPlugin.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace JellyfinStreamingPlugin;

/// <summary>
/// Jellyfin discovers this class via reflection and calls RegisterServices at startup.
/// This is where we hook into Jellyfin's DI container.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Register the TMDB HTTP client with a 30s timeout
        serviceCollection.AddHttpClient("TmdbClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "JellyfinStreamingPlugin/1.0");
        });

        // Core services
        serviceCollection.AddSingleton<TmdbService>();
        serviceCollection.AddSingleton<WatchProviderService>();

        // The channel — must be registered as IChannel for Jellyfin to find it
        serviceCollection.AddSingleton<IChannel, StreamingChannel>();

        // Background sync task — must be registered as IScheduledTask
        serviceCollection.AddSingleton<IScheduledTask, SyncStreamingContentTask>();
    }
}
