namespace ServiceBusConsole.Abstractions;

/// <summary>An Azure subscription to browse; <see cref="Id"/> null is the local emulator.</summary>
public sealed record AzureSubscription(string Name, string? Id)
{
    public static AzureSubscription Emulator { get; } = new("Local Emulator", null);

    public bool IsEmulator => Id is null;
}
