namespace ServiceBusConsole.Plugins;

/// <summary>Names the <see cref="IPlugin"/> of a <c>ServiceBusConsole.Plugins.*</c> assembly; its type needs a parameterless constructor.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ServiceBusConsolePluginAttribute(Type pluginType) : Attribute
{
    public Type PluginType { get; } = pluginType;
}
