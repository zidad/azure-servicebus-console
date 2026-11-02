namespace ServiceBusConsole.PluginHost;

/// <summary>An action that can run on the message in view, with its plugin.</summary>
public sealed record OfferedAction(IPlugin Plugin, IMessageAction Action);
