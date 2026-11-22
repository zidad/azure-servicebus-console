using System.CommandLine;
using System.CommandLine.Help;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.CommandLine;

/// <summary>
/// The azsb commands: <see cref="RunCommand"/>, the default, the namespace commands (<see cref="BrokerCommand"/>),
/// <see cref="ConfigCommand"/>, <see cref="PluginsCommand"/> and help.
/// System.CommandLine parses without reflection, so the app also runs as a Native AOT binary.
/// </summary>
public sealed class AppCommands
{
    private readonly IHostBuilder _host;
    private readonly PluginCatalog _plugins;
    private readonly BrokerTransport _transport;
    private readonly Func<CommandServices> _services;

    // In the transport's words: its names for the levels.
    private readonly Option<string?> _connection;
    private readonly Option<string?> _broker;
    private readonly Option<string?> _open;

    private readonly Option<string?> _theme = new("--theme", "-t")
    {
        Description = CommandLineStrings.AppCommands_ThemeOption,
    };

    private readonly Argument<ConfigAction> _action = new("action") { Description = CommandLineStrings.AppCommands_ActionArgument };
    private readonly Argument<string?> _key = new("key") { Description = CommandLineStrings.AppCommands_KeyArgument, Arity = ArgumentArity.ZeroOrOne };
    private readonly Argument<string?> _value = new("value") { Description = CommandLineStrings.AppCommands_ValueArgument, Arity = ArgumentArity.ZeroOrOne };
    private readonly Option<bool> _all = new("--all") { Description = CommandLineStrings.AppCommands_AllOption };
    private readonly Argument<string[]> _topic = new("command") { Description = CommandLineStrings.AppCommands_CommandArgument, Arity = ArgumentArity.ZeroOrMore };

    // Namespace commands.
    private readonly Option<string?> _scriptConnection;
    private readonly Option<string?> _scriptBroker;
    private readonly Option<bool> _json = new("--json") { Description = CommandLineStrings.AppCommands_JsonOption };
    private readonly Option<string?> _confirm;
    private readonly Argument<ListKind?> _listKind = new("kind") { Description = CommandLineStrings.AppCommands_ListKindArgument, Arity = ArgumentArity.ZeroOrOne, HelpName = "queues|topics|subscriptions" };
    private readonly Option<bool> _deadLetters = new("--dead-letters") { Description = CommandLineStrings.AppCommands_DeadLettersOption };
    private readonly Argument<string> _entity = new("entity") { Description = CommandLineStrings.AppCommands_EntityArgument };
    private readonly Argument<string> _sendTarget = new("entity") { Description = CommandLineStrings.AppCommands_SendTargetArgument };
    private readonly Option<bool> _dlq = new("--dlq") { Description = CommandLineStrings.AppCommands_DlqOption };
    private readonly Option<int> _max = new("--max") { Description = CommandLineStrings.AppCommands_MaxOption, DefaultValueFactory = _ => PeekCommand.DefaultMax };
    private readonly Option<long?> _fromSequence = new("--from-sequence") { Description = CommandLineStrings.AppCommands_FromSequenceOption };
    private readonly Option<bool> _full = new("--full") { Description = CommandLineStrings.AppCommands_FullOption };
    private readonly Option<string[]> _messageIds = new("--message-id") { Description = CommandLineStrings.AppCommands_MessageIdOption, AllowMultipleArgumentsPerToken = true };
    private readonly Option<long[]> _sequences = new("--sequence") { Description = CommandLineStrings.AppCommands_SequenceOption, AllowMultipleArgumentsPerToken = true };
    private readonly Option<bool> _allMessages = new("--all") { Description = CommandLineStrings.AppCommands_AllMessagesOption };
    private readonly Option<int> _limit = new("--limit") { Description = CommandLineStrings.AppCommands_LimitOption, DefaultValueFactory = _ => MessageSelection.DefaultLimit };
    private readonly Option<bool> _dryRun = new("--dry-run") { Description = CommandLineStrings.AppCommands_DryRunOption };
    private readonly Option<bool> _yes = new("--yes") { Description = CommandLineStrings.AppCommands_YesOption };
    private readonly Option<string?> _body = new("--body") { Description = CommandLineStrings.AppCommands_BodyOption };
    private readonly Option<string?> _file = new("--file") { Description = CommandLineStrings.AppCommands_FileOption };
    private readonly Option<string?> _messageId = new("--message-id") { Description = CommandLineStrings.AppCommands_SendMessageIdOption };
    private readonly Option<string?> _contentType = new("--content-type") { Description = CommandLineStrings.AppCommands_ContentTypeOption };
    private readonly Option<string?> _subject = new("--subject") { Description = CommandLineStrings.AppCommands_SubjectOption };
    private readonly Option<string?> _correlationId = new("--correlation-id") { Description = CommandLineStrings.AppCommands_CorrelationIdOption };
    private readonly Option<string[]> _properties = new("--property") { Description = CommandLineStrings.AppCommands_PropertyOption };

    private readonly Command _run;
    private readonly Command _config;
    private readonly Command _pluginsCommand;
    private readonly Command _check;
    private readonly Command _list;
    private readonly Command _peek;
    private readonly Command _retry;
    private readonly Command _delete;
    private readonly Command _purge;
    private readonly Command _send;


    /// <param name="transport">The broker; none when omitted, for commands that never open one.</param>
    /// <param name="services">What the namespace commands use; by default the live broker without logging.</param>
    public AppCommands(IHostBuilder host, PluginCatalog plugins, BrokerTransport? transport = null, Func<CommandServices>? services = null)
    {
        _host = host;
        _plugins = plugins;
        _transport = transport ?? BrokerTransport.None;
        var terms = _transport.Terms;
        _connection = LevelOption(terms.Connection, "connection", terms.Connection.OptionHelp);
        _broker = LevelOption(terms.Broker, "broker", terms.Broker.OptionHelp);
        _open = new("--open", "-o") { Description = CommandLineStrings.FormatAppCommands_OpenOption(terms.Broker.OneInText, $"--{terms.Broker.Option}") };
        _scriptConnection = LevelOption(terms.Connection, "connection", terms.Connection.ScriptOptionHelp);
        _scriptBroker = LevelOption(terms.Broker, "broker", terms.Broker.ScriptOptionHelp);
        _confirm = new("--confirm") { Description = CommandLineStrings.FormatAppCommands_ConfirmOption(terms.Broker.OneInText) };
        _services = services ?? (() => CommandServices.Create(_transport, logging => logging.AddProvider(NullLoggerProvider.Instance)));
        _run = new("run", CommandLineStrings.AppCommands_Run) { _connection, _broker, _open, _theme };
        _config = new("config", CommandLineStrings.AppCommands_Config) { _action, _key, _value, _all };
        _pluginsCommand = new("plugins", CommandLineStrings.AppCommands_Plugins);
        _check = Namespace(new("check", CommandLineStrings.FormatAppCommands_Check(terms.Broker.OneInText)));
        _list = Namespace(new("list", CommandLineStrings.AppCommands_List) { _listKind, _deadLetters });
        _peek = Namespace(new("peek", CommandLineStrings.AppCommands_Peek) { _entity, _dlq, _max, _fromSequence, _full });
        _retry = Namespace(new("retry", CommandLineStrings.AppCommands_Retry) { _entity, _messageIds, _sequences, _allMessages, _limit, _dryRun, _confirm });
        _delete = Namespace(new("delete", CommandLineStrings.AppCommands_Delete) { _entity, _dlq, _messageIds, _sequences, _allMessages, _limit, _dryRun, _confirm });
        _purge = Namespace(new("purge", CommandLineStrings.AppCommands_Purge) { _entity, _dlq, _yes, _confirm });
        _send = Namespace(new("send", CommandLineStrings.AppCommands_Send)
        {
            _sendTarget, _body, _file, _messageId, _contentType, _subject, _correlationId, _properties, _confirm,
        });
        var help = new Command("help", CommandLineStrings.AppCommands_Help) { _topic };

        // Named azsb, not after the executable (ServiceBusConsole.Azsb).
        Root = new("azsb", CommandLineStrings.AppCommands_Root)
        {
            new HelpOption(), new VersionOption(), _connection, _broker, _open, _theme, _run,
            _check, _list, _peek, _retry, _delete, _purge, _send, _config, _pluginsCommand, help,
        };
        foreach (var command in new[] { Root, _run, _config, _pluginsCommand, _check, _list, _peek, _retry, _delete, _purge, _send })
            command.SetAction((result, ct) => ExecuteAsync(result, ct));
        help.SetAction(result => Root.Parse([.. result.GetValue(_topic) ?? [], "--help"]).Invoke(new() { Output = result.InvocationConfiguration.Output }));
    }

    public Command Root { get; }

    // The transport's name for the level, with its short alias and, when it differs, the generic name.
    private static Option<string?> LevelOption(LevelTerm term, string generic, string help) =>
        new($"--{term.Option}", [$"-{term.ShortOption}", .. term.Option == generic ? Array.Empty<string>() : [$"--{generic}"]]) { Description = help };

    public static Task<int> ExecuteAsync(IHostBuilder host, PluginCatalog plugins, BrokerTransport transport, Func<CommandServices> services, string[] args) =>
        new AppCommands(host, plugins, transport, services).Root.Parse(args).InvokeAsync();

    /// <summary>What <paramref name="args"/> ask for; null for help or invalid usage.</summary>
    public CommandInput? Bind(params string[] args) => Bind(Root.Parse(args));

    private Command Namespace(Command command)
    {
        command.Add(_scriptConnection);
        command.Add(_scriptBroker);
        command.Add(_json);
        return command;
    }

    private CommandInput? Bind(ParseResult result) => result.Errors.Count > 0 ? null : result.CommandResult.Command switch
    {
        var c when c == _config => new ConfigInput
        {
            Action = result.GetValue(_action),
            Key = result.GetValue(_key),
            Value = result.GetValue(_value),
            All = result.GetValue(_all),
            Keys = ConfigKeys.With([.. _transport.Settings, .. _plugins.ConfigKeys]),
        },
        var c when c == _pluginsCommand => new PluginsInput
        {
            Catalog = _plugins,
            Configuration = new ConfigurationBuilder().AddAzsbConfiguration(AppPaths.ConfigFile).Build(),
        },
        var c when c == _run || c == Root => new RunInput
        {
            HostBuilder = _host,
            Connection = result.GetValue(_connection),
            Broker = result.GetValue(_broker),
            Open = result.GetValue(_open),
            Theme = result.GetValue(_theme),
        },
        var c when BrokerCommandOf(c, result) is { } command => new BrokerInput
        {
            Command = command,
            Target = new(result.GetValue(_scriptConnection), result.GetValue(_scriptBroker)),
            Json = result.GetValue(_json),
            Confirm = result.GetValue(_confirm),
            Services = _services,
        },
        _ => null,
    };

    private BrokerCommand? BrokerCommandOf(Command command, ParseResult result) => command switch
    {
        var c when c == _check => new CheckCommand(),
        var c when c == _list => new ListCommand(result.GetValue(_listKind) ?? ListKind.Entities, result.GetValue(_deadLetters)),
        var c when c == _peek => new PeekCommand(
            result.GetValue(_entity) ?? "", result.GetValue(_dlq), result.GetValue(_max), result.GetValue(_fromSequence), result.GetValue(_full)),
        var c when c == _retry => new RetryCommand(result.GetValue(_entity) ?? "", Selection(result), result.GetValue(_dryRun)),
        var c when c == _delete => new DeleteCommand(result.GetValue(_entity) ?? "", result.GetValue(_dlq), Selection(result), result.GetValue(_dryRun)),
        var c when c == _purge => new PurgeCommand(result.GetValue(_entity) ?? "", result.GetValue(_dlq), result.GetValue(_yes)),
        var c when c == _send => new SendCommand(result.GetValue(_sendTarget) ?? "")
        {
            Body = result.GetValue(_body),
            File = result.GetValue(_file),
            MessageId = result.GetValue(_messageId) ?? "",
            ContentType = result.GetValue(_contentType) ?? "",
            Subject = result.GetValue(_subject) ?? "",
            CorrelationId = result.GetValue(_correlationId) ?? "",
            Properties = result.GetValue(_properties) ?? [],
        },
        _ => null,
    };

    private MessageSelection Selection(ParseResult result) => new()
    {
        MessageIds = result.GetValue(_messageIds) ?? [],
        SequenceNumbers = result.GetValue(_sequences) ?? [],
        All = result.GetValue(_allMessages),
        Limit = result.GetValue(_limit),
    };

    private async Task<int> ExecuteAsync(ParseResult result, CancellationToken ct) =>
        Bind(result) is { } input
            ? await input.Match(
                async run => await RunCommand.ExecuteAsync(run) ? ExitCodes.Ok : ExitCodes.Failed,
                config => Task.FromResult(ConfigCommand.Execute(config) ? ExitCodes.Ok : ExitCodes.Failed),
                plugins => Task.FromResult(PluginsCommand.Execute(plugins) ? ExitCodes.Ok : ExitCodes.Failed),
                ns => BrokerCommand.ExecuteAsync(ns, ct))
            : ExitCodes.Failed;
}
