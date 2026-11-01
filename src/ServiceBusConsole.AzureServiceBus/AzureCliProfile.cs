using System.Text.Json;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>The Azure CLI's profile: which subscription <c>az account set</c> made current.</summary>
internal static class AzureCliProfile
{
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetEnvironmentVariable("AZURE_CONFIG_DIR") is { Length: > 0 } dir
                ? dir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure"),
            "azureProfile.json");

    /// <summary>The <c>isDefault</c> subscription's ID; null without a profile or a default.</summary>
    public static async Task<string?> CurrentSubscriptionIdAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        using var profile = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return profile.RootElement.TryGetProperty("subscriptions", out var subscriptions) && subscriptions.ValueKind == JsonValueKind.Array
            ? subscriptions.EnumerateArray()
                .Where(s => s.TryGetProperty("isDefault", out var isDefault) && isDefault.ValueKind == JsonValueKind.True)
                .Select(s => s.TryGetProperty("id", out var id) ? id.GetString() : null)
                .FirstOrDefault()
            : null;
    }
}
