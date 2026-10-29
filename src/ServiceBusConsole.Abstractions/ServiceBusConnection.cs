namespace ServiceBusConsole.Abstractions;

/// <summary>The namespace being browsed; the services act on it.</summary>
public sealed class ServiceBusConnection
{
    public NamespaceInfo? Namespace { get; private set; }
    public string? CurrentNamespace => Namespace?.FullyQualifiedNamespace;
    public bool IsEmulator => CurrentNamespace == NamespaceInfo.Emulator.FullyQualifiedNamespace;

    public void SetNamespace(NamespaceInfo ns) => Namespace = ns;
}
