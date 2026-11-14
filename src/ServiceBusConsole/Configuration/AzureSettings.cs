using Microsoft.Extensions.Configuration;
using ServiceBusConsole.AzureServiceBus;

namespace ServiceBusConsole.Configuration;

/// <summary>The <see cref="AzureOptions"/> that <see cref="ConfigKeys.AzureAuth"/> and <see cref="ConfigKeys.AzureTransport"/> set.</summary>
public static class AzureSettings
{
    /// <exception cref="CommandFailure">A value isn't one the key allows (usage exit code).</exception>
    public static AzureOptions Read(IConfiguration configuration) => new()
    {
        SignIn = Parse<AzureSignIn>(configuration, ConfigKeys.AzureAuth),
        Transport = Parse<ServiceBusTransport>(configuration, ConfigKeys.AzureTransport),
    };

    private static T Parse<T>(IConfiguration configuration, ConfigKey key) where T : struct, Enum =>
        (configuration[key.ConfigurationPath] ?? key.Default ?? "") is var value
        && Enum.GetNames<T>().FirstOrDefault(n => n.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) is { } name
            ? Enum.Parse<T>(name)
            : throw new CommandFailure(ExitCodes.Usage, ConfigurationStrings.FormatAzureSettings_Invalid(
                value, key.Key, string.Join(" | ", Enum.GetNames<T>().Select(n => n.ToLowerInvariant()))));
}
