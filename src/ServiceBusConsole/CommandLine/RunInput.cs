using JasperFx.CommandLine;

namespace ServiceBusConsole.CommandLine;

public sealed class RunInput : NetCoreInput
{
    [Description("Azure subscription to start in, by name or ID; emulator for the local emulator")]
    public string? SubscriptionFlag { get; set; }

    [Description("Namespace of that subscription to open, by name or host name; needs --subscription")]
    public string? NamespaceFlag { get; set; }

    /// <summary>Where the app starts; null for the connection page as usual.</summary>
    public StartupTarget? StartupTarget => SubscriptionFlag is { } subscription ? new(subscription, NamespaceFlag) : null;
}
