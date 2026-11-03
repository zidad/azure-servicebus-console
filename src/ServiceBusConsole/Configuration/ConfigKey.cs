namespace ServiceBusConsole.Configuration;

/// <summary>A setting azsb knows: its dotted key, what it does, and the value it has when unset.</summary>
public sealed record ConfigKey(string Key, string Description, string? Default = null)
{
    /// <summary>The key as <c>IConfiguration</c> reads it: <c>ui:theme</c>.</summary>
    public string ConfigurationPath => Key.Replace('.', ':');
}
