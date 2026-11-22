namespace ServiceBusConsole.UI;

/// <summary>The transport's words, chosen once at startup like the <see cref="Theme"/>; what the UI and command line call each level.</summary>
public static class Terms
{
    public static BrokerTerms Current { get; private set; } = BrokerTerms.Generic;

    public static void Use(BrokerTerms terms) => Current = terms;

    public static LevelTerm Connection => Current.Connection;

    public static LevelTerm Broker => Current.Broker;

    /// <summary><c>--namespace</c>: the option naming a broker.</summary>
    public static string BrokerOption => $"--{Broker.Option}";

    /// <summary><c>--subscription</c>: the option naming a connection.</summary>
    public static string ConnectionOption => $"--{Connection.Option}";
}
