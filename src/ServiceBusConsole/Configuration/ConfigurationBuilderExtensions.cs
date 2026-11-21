using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Configuration;

public static class ConfigurationBuilderExtensions
{
    /// <summary>The config file, then the app's environment variables (<c>AZSB_UI__THEME</c>) over it.</summary>
    public static IConfigurationBuilder AddAppConfiguration(this IConfigurationBuilder builder, AppIdentity app, string? configFile = null) =>
        builder
            .AddJsonFile(configFile ?? app.ConfigFile, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(app.EnvironmentPrefix);
}
