using ServiceBusConsole.Abstractions.Resources;

namespace ServiceBusConsole.Abstractions;

/// <summary>A transport's words, wherever the UI and command line name its levels or tell how to sign in.</summary>
/// <param name="Connection">What lists brokers: Service Bus's subscription, RabbitMQ's server.</param>
/// <param name="Broker">What is browsed: Service Bus's namespace, RabbitMQ's virtual host.</param>
/// <param name="SignInHint">What to do when listing connections fails for want of credentials: <c>Sign in with az login</c>.</param>
/// <param name="NoDefaultConnection">Why a command without the connection option has none: the Azure CLI has no current subscription.</param>
public sealed record BrokerTerms(LevelTerm Connection, LevelTerm Broker, string SignInHint, string NoDefaultConnection)
{
    /// <summary>For an app without a transport, such as tests of commands that never open a broker.</summary>
    public static BrokerTerms Generic { get; } = new(
        new(Strings.BrokerTerms_Connection, Strings.BrokerTerms_Connections, Strings.BrokerTerms_ConnectionInText, Strings.BrokerTerms_ConnectionsInText,
            "🔌", "connection", 'c', Strings.BrokerTerms_ConnectionOption, Strings.BrokerTerms_ConnectionScriptOption),
        new(Strings.BrokerTerms_Broker, Strings.BrokerTerms_Brokers, Strings.BrokerTerms_BrokerInText, Strings.BrokerTerms_BrokersInText,
            "📦", "broker", 'b', Strings.BrokerTerms_BrokerOption, Strings.BrokerTerms_BrokerScriptOption),
        Strings.BrokerTerms_SignInHint,
        Strings.BrokerTerms_NoDefaultConnection);
}
