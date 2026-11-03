using JasperFx.CommandLine;
using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.CommandLine;

public sealed class RunInput : NetCoreInput
{
    [Description("Azure subscription to start in, by name or ID; emulator for the local emulator")]
    public string? SubscriptionFlag { get; set; }

    [Description("Namespace of that subscription to open, by name or host name; needs --subscription")]
    public string? NamespaceFlag { get; set; }

    [Description("Colors for a dark or light terminal background: auto (default; COLORFGBG, else dark), dark or light; overrides ui.theme")]
    public string? ThemeFlag { get; set; }

    /// <summary>The <c>--theme</c> flag, else <c>ui.theme</c> from the environment or config file; null for auto.</summary>
    public string? ThemeSetting(IConfiguration configuration) => ThemeFlag ?? configuration[ConfigKeys.Theme.ConfigurationPath];

    /// <summary>Where the app starts; null for the connection page as usual.</summary>
    public StartupTarget? StartupTarget => SubscriptionFlag is { } subscription ? new(subscription, NamespaceFlag) : null;
}
