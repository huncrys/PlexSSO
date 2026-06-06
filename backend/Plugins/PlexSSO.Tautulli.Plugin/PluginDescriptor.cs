using Microsoft.Extensions.DependencyInjection;
using PlexSSO.Plugin;
using PlexSSO.Service;
using PlexSSO.Service.Config;
using PlexSSO.Tautulli.Plugin.Model;
using PlexSSO.Tautulli.Plugin.TautulliClient;

namespace PlexSSO.Tautulli.Plugin
{
    public class PluginDescriptor : IPlugin
    {
        public string Name { get; } = TautulliConstants.PluginName;

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<ITokenService, TautulliTokenService>();
        }

        public void ConfigurePlugin(IConfigurationService configurationService)
        {
            var config = configurationService.GetPluginConfig<TautulliConfig>(TautulliConstants.PluginName);
            var publicHostname = configurationService.Config
                .TryGetPluginConfigValue(TautulliConstants.PluginName, TautulliConstants.PublicHostname);
            var apiKey = configurationService.Config
                .TryGetPluginConfigValue(TautulliConstants.PluginName, TautulliConstants.ApiKey);

            var changed = false;
            if (string.IsNullOrWhiteSpace(config.PublicHostname) &&
                !string.IsNullOrWhiteSpace(publicHostname))
            {
                config.PublicHostname = publicHostname;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(config.ApiKey) &&
                !string.IsNullOrWhiteSpace(apiKey))
            {
                config.ApiKey = apiKey;
                changed = true;
            }

            if (changed)
            {
                configurationService.SavePluginConfig(TautulliConstants.PluginName, config);
            }
        }
    }
}
