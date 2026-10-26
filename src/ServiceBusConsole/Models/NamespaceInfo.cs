namespace ServiceBusConsole.Models;

public class NamespaceInfo
{
    public required string Name { get; set; }
    public required string FullyQualifiedNamespace { get; set; }
    public required string ResourceGroup { get; set; }
    public required string Location { get; set; }
    // Null for the emulator and for namespaces cached before these were recorded.
    public string? Sku { get; set; }
    public string? Status { get; set; }
}
