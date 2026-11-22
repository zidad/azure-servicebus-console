namespace ServiceBusConsole.Abstractions;

/// <summary>What a transport calls one level of its hierarchy: Service Bus's subscription or namespace, RabbitMQ's server or virtual host.</summary>
/// <param name="One">As a label: <c>Namespace</c>.</param>
/// <param name="Many">As a label: <c>Namespaces</c>.</param>
/// <param name="OneInText">Inside a sentence: <c>namespace</c>.</param>
/// <param name="ManyInText">Inside a sentence: <c>namespaces</c>.</param>
/// <param name="Icon">Shown before a name.</param>
/// <param name="Option">The command-line option naming one, without dashes: <c>namespace</c>.</param>
/// <param name="ShortOption">Its one-letter alias: <c>n</c>.</param>
/// <param name="OptionHelp">Help for the option on the command that opens the terminal UI.</param>
/// <param name="ScriptOptionHelp">Help for the option on the scripting commands, with its default.</param>
public sealed record LevelTerm(
    string One, string Many, string OneInText, string ManyInText, string Icon, string Option, char ShortOption, string OptionHelp, string ScriptOptionHelp);
