using System.Text.Json.Serialization;

namespace ServiceBusConsole.Abstractions;

public sealed record BrokerInfo
{
    public static BrokerInfo Emulator { get; } = new() { Name = "localhost", Address = "localhost" };

    public required string Name { get; init; }
    // Its cached name from before it was renamed.
    [JsonPropertyName("FullyQualifiedNamespace")]
    public required string Address { get; init; }

    // Set, not init: the generated cache context would leave it null for a listing cached before it existed.
    public IReadOnlyList<BrokerProperty> Properties { get; set; } = [];

    public string? Property(string id) => Properties.FirstOrDefault(p => p.Id == id)?.Value;

    public bool Equals(BrokerInfo? other) =>
        other is not null && Name == other.Name && Address == other.Address && Properties.SequenceEqual(other.Properties);

    public override int GetHashCode() => HashCode.Combine(Name, Address);
}
