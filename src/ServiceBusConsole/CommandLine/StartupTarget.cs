namespace ServiceBusConsole.CommandLine;

/// <summary>Where the command line asks the app to start: a subscription, and optionally one of its namespaces.</summary>
public sealed record StartupTarget(string Subscription, string? Namespace = null)
{
    private const string EmulatorAlias = "emulator";

    /// <summary>By ID or display name, ignoring case; <c>emulator</c> also names the local emulator.</summary>
    public bool Matches(AzureSubscription subscription) =>
        string.Equals(subscription.Id, Subscription, StringComparison.OrdinalIgnoreCase)
        || string.Equals(subscription.Name, Subscription, StringComparison.OrdinalIgnoreCase)
        || (subscription.IsEmulator && string.Equals(Subscription, EmulatorAlias, StringComparison.OrdinalIgnoreCase));

    /// <summary>By name or fully qualified host name, ignoring case; never when no namespace was asked for.</summary>
    public bool Matches(NamespaceInfo ns) =>
        Namespace is { } name
        && (string.Equals(ns.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ns.FullyQualifiedNamespace, name, StringComparison.OrdinalIgnoreCase));
}
