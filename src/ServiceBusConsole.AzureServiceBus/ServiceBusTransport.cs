namespace ServiceBusConsole.AzureServiceBus;

/// <summary>How messages travel to an Azure namespace; the emulator always uses TCP.</summary>
public enum ServiceBusTransport
{
    /// <summary>AMQP on port 5671.</summary>
    Tcp,

    /// <summary>AMQP over WebSockets on port 443, for networks that block 5671 (mobile, corporate proxies).</summary>
    WebSockets,
}
