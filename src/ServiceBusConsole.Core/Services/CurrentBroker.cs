namespace ServiceBusConsole.Core.Services;

/// <summary>
/// The broker being browsed. Choosing another opens it and disposes the previous one in the background; pages read
/// <see cref="Broker"/> each time, so a broker reopened in place (its live listing replacing a cached one) takes effect.
/// </summary>
public sealed class CurrentBroker(IBrokerOpener opener) : IAsyncDisposable
{
    /// <summary>Null until a broker is chosen.</summary>
    public IBroker? Broker { get; private set; }

    /// <summary>The current broker, for work that needs one.</summary>
    public IBroker Opened => Broker ?? throw new InvalidOperationException(ServiceStrings.CurrentBroker_None);

    public string? Address => Broker?.Info.Address;

    /// <summary>Opens <paramref name="info"/>, unless it is open already.</summary>
    public void Set(BrokerInfo info)
    {
        if (Broker?.Info == info) return;
        var previous = Broker;
        Broker = opener.Open(info);
        if (previous is not null) _ = previous.DisposeAsync().AsTask();
    }

    public async ValueTask DisposeAsync()
    {
        if (Broker is { } broker) await broker.DisposeAsync();
        Broker = null;
    }
}
