namespace ServiceBusConsole.Abstractions;

public sealed record BrokerInfo
{
    public static BrokerInfo Emulator { get; } = new() { Name = "localhost", FullyQualifiedNamespace = "localhost" };

    public required string Name { get; init; }
    public required string FullyQualifiedNamespace { get; init; }

    // Set, not init: the generated cache context would leave it null for a listing cached before it existed.
    public IReadOnlyList<BrokerProperty> Properties { get; set; } = [];

    public string? Property(string id) => Properties.FirstOrDefault(p => p.Id == id)?.Value;

    public bool Equals(BrokerInfo? other) =>
        other is not null && Name == other.Name && FullyQualifiedNamespace == other.FullyQualifiedNamespace && Properties.SequenceEqual(other.Properties);

    public override int GetHashCode() => HashCode.Combine(Name, FullyQualifiedNamespace);
}
