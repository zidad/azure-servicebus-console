using ServiceBusConsole.AzureServiceBus.Resources;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>Service Bus's words: Azure subscriptions list namespaces.</summary>
public static class ServiceBusTerms
{
    public static BrokerTerms Terms { get; } = new(
        new(Strings.ServiceBusTerms_Subscription, Strings.ServiceBusTerms_Subscriptions, Strings.ServiceBusTerms_SubscriptionInText,
            Strings.ServiceBusTerms_SubscriptionsInText, "🔑", "subscription", 's',
            Strings.ServiceBusTerms_SubscriptionOption, Strings.ServiceBusTerms_SubscriptionScriptOption),
        new(Strings.ServiceBusTerms_Namespace, Strings.ServiceBusTerms_Namespaces, Strings.ServiceBusTerms_NamespaceInText,
            Strings.ServiceBusTerms_NamespacesInText, "🌐", "namespace", 'n',
            Strings.ServiceBusTerms_NamespaceOption, Strings.ServiceBusTerms_NamespaceScriptOption),
        Strings.ServiceBusTerms_SignInHint,
        Strings.ServiceBusTerms_NoDefaultSubscription);
}
