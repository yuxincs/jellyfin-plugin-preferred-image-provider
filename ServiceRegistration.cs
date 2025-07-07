using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using JellyfinPreferredImageProvider.Providers;

namespace JellyfinPreferredImageProvider
{
    /// <summary>
    /// Service registration class.
    /// </summary>
    public static class ServiceRegistration
    {
        /// <summary>
        /// Registers services with the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public static void RegisterServices(IServiceCollection services)
        {
            // Register our single preferred image provider
            services.AddTransient<IRemoteImageProvider, PreferredImageProvider>();
        }
    }
}