namespace ServiceBusConsole.Services;

/// <summary>
/// The namespace being browsed. Choosing another opens it and disposes the previous one in the background; pages read
/// <see cref="Current"/> each time, so a namespace reopened in place (its live listing replacing a cached one) takes effect.
/// </summary>
public sealed class ServiceBusConnection(INamespaceOpener opener) : IAsyncDisposable
{
    /// <summary>Null until a namespace is chosen.</summary>
    public IServiceBusNamespace? Current { get; private set; }

    /// <summary>The current namespace, for work that needs one.</summary>
    public IServiceBusNamespace Namespace => Current ?? throw new InvalidOperationException(ServiceStrings.ServiceBusConnection_NoNamespace);

    public string? CurrentNamespace => Current?.Info.FullyQualifiedNamespace;

    /// <summary>Opens <paramref name="info"/>, unless it is open already.</summary>
    public void SetNamespace(NamespaceInfo info)
    {
        if (Current?.Info == info) return;
        var previous = Current;
        Current = opener.Open(info);
        if (previous is not null) _ = previous.DisposeAsync().AsTask();
    }

    public async ValueTask DisposeAsync()
    {
        if (Current is { } current) await current.DisposeAsync();
        Current = null;
    }
}
