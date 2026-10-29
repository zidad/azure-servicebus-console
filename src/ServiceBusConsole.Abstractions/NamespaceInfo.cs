namespace ServiceBusConsole.Abstractions;

public sealed record NamespaceInfo
{
    public static NamespaceInfo Emulator { get; } = new()
    {
        Name = "localhost", FullyQualifiedNamespace = "localhost", ResourceGroup = "", Location = "",
    };

    public required string Name { get; init; }
    public required string FullyQualifiedNamespace { get; init; }
    public required string ResourceGroup { get; init; }
    public required string Location { get; init; }
    // Null for the emulator and for namespaces cached before these were recorded.
    public string? Sku { get; init; }
    public string? Status { get; init; }
}
