using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Configuration;

public static class ConfigurationBuilderExtensions
{
    public const string EnvironmentPrefix = "AZSB_";

    /// <summary>The config file, then <c>AZSB_</c> environment variables (<c>AZSB_UI__THEME</c>) over it.</summary>
    public static IConfigurationBuilder AddAzsbConfiguration(this IConfigurationBuilder builder, string configFile) =>
        builder
            .AddJsonFile(configFile, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(EnvironmentPrefix);
}
