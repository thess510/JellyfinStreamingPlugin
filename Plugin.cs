using System;
using System.Collections.Generic;
using JellyfinStreamingPlugin.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace JellyfinStreamingPlugin;

/// <summary>
/// Main plugin class. Jellyfin discovers this via reflection — it must inherit BasePlugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Streaming Discovery";

    /// <inheritdoc />
    public override Guid Id => new("4a8b1c2d-3e4f-5a6b-7c8d-9e0f1a2b3c4d");

    /// <inheritdoc />
    public override string Description =>
        "Browse and search external streaming services (Hulu, Netflix, Disney+, etc.) directly inside Jellyfin.";

    /// <summary>
    /// Exposes the plugin config page in the Jellyfin admin UI.
    /// The HTML file is embedded as a resource in the compiled dll.
    /// </summary>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            }
        };
    }
}
