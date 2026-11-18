namespace ServiceBusConsole.Abstractions;

public interface IBrokerOpener
{
    /// <summary>The namespace with the features its kind and tier have; connects on first use.</summary>
    IBroker Open(BrokerInfo info);
}
