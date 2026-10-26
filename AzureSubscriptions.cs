namespace ServiceBusConsole;

/// <summary>An Azure subscription to browse; <see cref="Id"/> null is the local emulator.</summary>
public sealed record AzureSubscription(string Name, string? Id)
{
    public bool IsEmulator => Id is null;
}

public static class AzureSubscriptions
{
    public static readonly AzureSubscription[] All =
    [
        new("Local Emulator", null),
        new("dev", "47ec78b6-8202-44cd-b2ea-adce486f7ff2"),
        new("tst", "f20969f7-2f17-4932-a7af-359e073160f2"),
        new("act", "365d1707-ec13-4e11-ad0a-919e5240ef03"),
        new("prd", "b8dbcf16-ed76-4c29-932b-b93ce96b0e6c"),
    ];

    public static NamespaceInfo EmulatorNamespace { get; } = new()
    {
        Name = "localhost", FullyQualifiedNamespace = "localhost", ResourceGroup = "", Location = "",
    };

    /// <summary>Namespaces of <paramref name="subscription"/>: cached first, then live.</summary>
    public static async IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(
        this INamespaceService service, AzureSubscription subscription,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (subscription.Id is null)
        {
            yield return EmulatorNamespace;
            yield break;
        }
        await foreach (var ns in service.GetNamespacesAsync(subscription.Id, ct))
            yield return ns;
    }
}
