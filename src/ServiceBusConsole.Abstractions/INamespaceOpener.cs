namespace ServiceBusConsole.Abstractions;

public interface INamespaceOpener
{
    /// <summary>The namespace with the features its kind and tier have; connects on first use.</summary>
    IServiceBusNamespace Open(NamespaceInfo info);
}
