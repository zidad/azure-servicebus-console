namespace ServiceBusConsole.Tests;

/// <summary>Service Bus's words as literal English, so tests catch text changes; the UI uses them for the whole session.</summary>
public static class TestTerms
{
    public static BrokerTerms ServiceBus { get; } = new(
        new("Subscription", "Subscriptions", "subscription", "subscriptions", "🔑", "subscription", 's',
            "Azure subscription to start in, by name or ID; emulator for the local emulator",
            "Azure subscription, by name or ID; emulator for the local emulator. Default: the Azure CLI's current subscription"),
        new("Namespace", "Namespaces", "namespace", "namespaces", "🌐", "namespace", 'n',
            "Namespace of that subscription to open, by name or host name; needs --subscription",
            "Namespace, by name, host name or a pattern with * that matches one. Default: the subscription's only namespace"),
        "Sign in with az login",
        "No --subscription, and the Azure CLI has no current subscription: run az login");

    /// <summary>A transport with these words and no broker, for command-line tests.</summary>
    public static BrokerTransport Transport { get; } = new(ServiceBus, [], (_, _) => { });

    [Before(TestSession)]
    public static void UseServiceBusTerms() => Terms.Use(ServiceBus);
}
