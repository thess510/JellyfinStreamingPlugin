using JellyfinStreamingPlugin.Channels;
using JellyfinStreamingPlugin.Services;
using JellyfinStreamingPlugin.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace JellyfinStreamingPlugin;

public class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient("TmdbClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "JellyfinStreamingPlugin/1.0");
        });

        // TmdbService is a regular class — fine to register
        serviceCollection.AddSingleton<TmdbService>();

        // WatchProviderService is static — no DI registration needed,
        // call its static methods directly
        
        serviceCollection.AddSingleton<IChannel, StreamingChannel>();
        serviceCollection.AddSingleton<IScheduledTask, SyncStreamingContentTask>();
    }
}
