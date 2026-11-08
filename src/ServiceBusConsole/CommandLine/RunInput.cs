using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.CommandLine;

public sealed record RunInput : CommandInput
{
    public required IHostBuilder HostBuilder { get; init; }

    /// <summary><c>--subscription</c>: by name or ID; <c>emulator</c> for the local emulator.</summary>
    public string? Subscription { get; init; }

    /// <summary><c>--namespace</c>: by name or host name; needs <see cref="Subscription"/>.</summary>
    public string? Namespace { get; init; }

    /// <summary><c>--theme</c>: auto, dark or light.</summary>
    public string? Theme { get; init; }

    /// <summary>The <c>--theme</c> flag, else <c>ui.theme</c> from the environment or config file; null for auto.</summary>
    public string? ThemeSetting(IConfiguration configuration) => Theme ?? configuration[ConfigKeys.Theme.ConfigurationPath];

    /// <summary>Where the app starts; null for the connection page as usual.</summary>
    public StartupTarget? StartupTarget => Subscription is { } subscription ? new(subscription, Namespace) : null;
}
