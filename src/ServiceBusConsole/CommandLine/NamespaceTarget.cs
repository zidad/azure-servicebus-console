namespace ServiceBusConsole.CommandLine;

/// <summary>
/// The namespace a command works on: <c>--subscription</c> by name or ID (else the Azure CLI's current one) and
/// <c>--namespace</c> by name, host name or a pattern (else the subscription's only namespace). A pattern must match one.
/// </summary>
public sealed record NamespaceTarget(string? Subscription = null, string? Namespace = null)
{
    public async Task<(AzureSubscription Subscription, NamespaceInfo Namespace)> ResolveAsync(INamespaceService service, CancellationToken ct)
    {
        var subscription = await FindSubscriptionAsync(service, ct);
        var namespaces = await service.GetNamespacesAsync(subscription, ct).ToListAsync(ct);
        return (subscription, PickNamespace(subscription, namespaces));
    }

    private async Task<AzureSubscription> FindSubscriptionAsync(INamespaceService service, CancellationToken ct)
    {
        var wanted = Subscription
            ?? await service.GetCurrentSubscriptionIdAsync(ct)
            ?? throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.NamespaceTarget_NoSubscription);
        var startup = new StartupTarget(wanted);
        if (startup.Matches(AzureSubscription.Emulator)) return AzureSubscription.Emulator;
        return await service.GetSubscriptionsAsync(ct).FirstOrDefaultAsync(startup.Matches, ct)
            ?? throw new CommandFailure(ExitCodes.NotFound, CommandLineStrings.FormatNamespaceTarget_SubscriptionNotFound(wanted));
    }

    private NamespaceInfo PickNamespace(AzureSubscription subscription, IReadOnlyList<NamespaceInfo> namespaces)
    {
        if (Namespace is not { } wanted)
            return namespaces switch
            {
                [var only] => only,
                [] => throw new CommandFailure(ExitCodes.NotFound, CommandLineStrings.FormatNamespaceTarget_NoNamespaces(subscription.Name)),
                _ => throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.FormatNamespaceTarget_PickNamespace(subscription.Name, Names(namespaces))),
            };

        var glob = new Glob(wanted);
        var startup = new StartupTarget(subscription.Name, wanted);
        var matches = namespaces.Where(ns => startup.Matches(ns) || (glob.IsPattern && (glob.Matches(ns.Name) || glob.Matches(ns.FullyQualifiedNamespace)))).ToList();
        return matches switch
        {
            [var one] => one,
            [] => throw new CommandFailure(ExitCodes.NotFound, CommandLineStrings.FormatNamespaceTarget_NamespaceNotFound(wanted, subscription.Name)),
            _ => throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.FormatNamespaceTarget_Ambiguous(wanted, Names(matches))),
        };
    }

    private static string Names(IEnumerable<NamespaceInfo> namespaces) => string.Join(", ", namespaces.Select(ns => ns.Name).Order(StringComparer.OrdinalIgnoreCase));
}
