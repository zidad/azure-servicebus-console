namespace ServiceBusConsole.CommandLine;

/// <summary>
/// The namespace a command works on: <c>--subscription</c> by name or ID (else the Azure CLI's current one) and
/// <c>--namespace</c> by name, host name or a pattern (else the subscription's only namespace). A pattern must match one.
/// </summary>
public sealed record BrokerTarget(string? Connection = null, string? Broker = null)
{
    public async Task<(ConnectionInfo Subscription, BrokerInfo Namespace)> ResolveAsync(IConnectionCatalog service, CancellationToken ct)
    {
        var subscription = await FindConnectionAsync(service, ct);
        var namespaces = await service.GetBrokersAsync(subscription, ct).ToListAsync(ct);
        return (subscription, PickBroker(subscription, namespaces));
    }

    private async Task<ConnectionInfo> FindConnectionAsync(IConnectionCatalog service, CancellationToken ct)
    {
        var wanted = Connection
            ?? await service.GetCurrentConnectionIdAsync(ct)
            ?? throw new CommandFailure(ExitCodes.Usage, Terms.Current.NoDefaultConnection);
        var startup = new StartupTarget(wanted);
        if (startup.Matches(ConnectionInfo.Emulator)) return ConnectionInfo.Emulator;
        return await service.GetConnectionsAsync(ct).FirstOrDefaultAsync(startup.Matches, ct)
            ?? throw new CommandFailure(ExitCodes.NotFound, CommandLineStrings.FormatBrokerTarget_ConnectionNotFound(Terms.Connection.OneInText, wanted));
    }

    private BrokerInfo PickBroker(ConnectionInfo subscription, IReadOnlyList<BrokerInfo> namespaces)
    {
        if (Broker is not { } wanted)
            return namespaces switch
            {
                [var only] => only,
                [] => throw new CommandFailure(ExitCodes.NotFound, CommandLineStrings.FormatBrokerTarget_NoBrokers(Terms.Connection.One, subscription.Name, Terms.Broker.ManyInText)),
                _ => throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.FormatBrokerTarget_PickBroker(Terms.Connection.One, subscription.Name, Terms.Broker.ManyInText, Terms.BrokerOption, Names(namespaces))),
            };

        var glob = new Glob(wanted);
        var startup = new StartupTarget(subscription.Name, wanted);
        var matches = namespaces.Where(ns => startup.Matches(ns) || (glob.IsPattern && (glob.Matches(ns.Name) || glob.Matches(ns.Address)))).ToList();
        return matches switch
        {
            [var one] => one,
            [] => throw new CommandFailure(ExitCodes.NotFound, CommandLineStrings.FormatBrokerTarget_BrokerNotFound(Terms.Broker.OneInText, wanted, Terms.Connection.OneInText, subscription.Name)),
            _ => throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.FormatBrokerTarget_Ambiguous(wanted, Terms.Broker.ManyInText, Names(matches))),
        };
    }

    private static string Names(IEnumerable<BrokerInfo> namespaces) => string.Join(", ", namespaces.Select(ns => ns.Name).Order(StringComparer.OrdinalIgnoreCase));
}
