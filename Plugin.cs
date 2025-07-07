using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using JellyfinPreferredImageProvider.Configuration;

namespace JellyfinPreferredImageProvider
{
    /// <summary>
    /// Preferred Image Provider plugin.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        /// <summary>
        /// Gets the plugin name.
        /// </summary>
        public override string Name => "Preferred Image Provider";

        /// <summary>
        /// Gets the plugin GUID.
        /// </summary>
        public override Guid Id => Guid.Parse("9993d9ad-27f4-4de7-9a9e-01317e9865f9");

        /// <summary>
        /// Gets the plugin description.
        /// </summary>
        public override string Description => "Intelligently selects poster, logo, and thumbnail images based on the original language of the content, vote count, and image resolution. Helps ensure that content displays culturally appropriate artwork.";

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static Plugin Instance { get; private set; }
    }
}