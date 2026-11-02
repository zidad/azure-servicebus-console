namespace ServiceBusConsole.PluginHost;

/// <param name="Missing">Why an unconfigured plugin is off, from <see cref="IPlugin.Missing"/>.</param>
public readonly record struct PluginStatus(bool Enabled, string? Missing)
{
    public override string ToString() => (Enabled, Missing) switch
    {
        (true, _) => "enabled",
        (false, { } missing) => $"disabled ({missing})",
        _ => "disabled",
    };
}
