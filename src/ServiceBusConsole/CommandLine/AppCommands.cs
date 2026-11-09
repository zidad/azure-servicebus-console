using System.CommandLine;
using System.CommandLine.Help;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.CommandLine;

/// <summary>
/// The azsb commands: <see cref="RunCommand"/>, the default, <see cref="ConfigCommand"/>, <see cref="PluginsCommand"/> and help.
/// System.CommandLine parses without reflection, so the app also runs as a Native AOT binary.
/// </summary>
public sealed class AppCommands
{
    private readonly IHostBuilder _host;
    private readonly PluginCatalog _plugins;

    private readonly Option<string?> _subscription = new("--subscription", "-s")
    {
        Description = "Azure subscription to start in, by name or ID; emulator for the local emulator",
    };

    private readonly Option<string?> _namespace = new("--namespace", "-n")
    {
        Description = "Namespace of that subscription to open, by name or host name; needs --subscription",
    };

    private readonly Option<string?> _open = new("--open", "-o")
    {
        Description = "Queue, topic or topic/subscription of that namespace to open, by exact name; /dlq or /active picks the tab. Or a list: queues, topics or subscriptions. Needs --namespace",
    };

    private readonly Option<string?> _theme = new("--theme", "-t")
    {
        Description = "Colors for a dark or light terminal background: auto (default; COLORFGBG, else dark), dark or light; overrides ui.theme",
    };

    private readonly Argument<ConfigAction> _action = new("action") { Description = "get, set, unset, list or path" };
    private readonly Argument<string?> _key = new("key") { Description = "Dotted key, such as ui.theme", Arity = ArgumentArity.ZeroOrOne };
    private readonly Argument<string?> _value = new("value") { Description = "Value to set", Arity = ArgumentArity.ZeroOrOne };
    private readonly Option<bool> _all = new("--all") { Description = "With list: also the known keys that are unset, with their default and description" };
    private readonly Argument<string[]> _topic = new("command") { Description = "The command to explain", Arity = ArgumentArity.ZeroOrMore };

    private readonly Command _run;
    private readonly Command _config;
    private readonly Command _pluginsCommand;

    public AppCommands(IHostBuilder host) : this(host, PluginCatalog.Empty) { }

    public AppCommands(IHostBuilder host, PluginCatalog plugins)
    {
        _host = host;
        _plugins = plugins;
        _run = new("run", "Open the terminal UI (the default command)") { _subscription, _namespace, _open, _theme };
        _config = new("config", """
            Read and write settings in the config file, like git config:
              get <key>: print a value, exit code 1 when unset; unset <key>: remove it
              set <key> <value>: true/false and integers are stored as such, the rest as text
              list [--all]: print key=value lines, secrets masked; path: print the config file's path
            """) { _action, _key, _value, _all };
        _pluginsCommand = new("plugins", "List the plugins, whether they are enabled, and their settings");
        var help = new Command("help", "Show help for azsb or one of its commands") { _topic };

        // Named azsb, not after the executable: the tool's shim runs ServiceBusConsole.
        Root = new("azsb", "Terminal UI for browsing Azure Service Bus namespaces, queues, topics, and messages")
        {
            new HelpOption(), new VersionOption(), _subscription, _namespace, _open, _theme, _run, _config, _pluginsCommand, help,
        };
        foreach (var command in new[] { Root, _run, _config, _pluginsCommand })
            command.SetAction(async (result, _) => await ExecuteAsync(result) ? 0 : 1);
        help.SetAction(result => Root.Parse([.. result.GetValue(_topic) ?? [], "--help"]).Invoke(new() { Output = result.InvocationConfiguration.Output }));
    }

    public Command Root { get; }

    public static Task<int> ExecuteAsync(IHostBuilder host, PluginCatalog plugins, string[] args) =>
        new AppCommands(host, plugins).Root.Parse(args).InvokeAsync();

    /// <summary>What <paramref name="args"/> ask for; null for help or invalid usage.</summary>
    public CommandInput? Bind(params string[] args) => Bind(Root.Parse(args));

    private CommandInput? Bind(ParseResult result) => result.Errors.Count > 0 ? null : result.CommandResult.Command switch
    {
        var c when c == _config => new ConfigInput
        {
            Action = result.GetValue(_action),
            Key = result.GetValue(_key),
            Value = result.GetValue(_value),
            All = result.GetValue(_all),
            Keys = ConfigKeys.With(_plugins.ConfigKeys),
        },
        var c when c == _pluginsCommand => new PluginsInput
        {
            Catalog = _plugins,
            Configuration = new ConfigurationBuilder().AddAzsbConfiguration(AppPaths.ConfigFile).Build(),
        },
        var c when c == _run || c == Root => new RunInput
        {
            HostBuilder = _host,
            Subscription = result.GetValue(_subscription),
            Namespace = result.GetValue(_namespace),
            Open = result.GetValue(_open),
            Theme = result.GetValue(_theme),
        },
        _ => null,
    };

    private Task<bool> ExecuteAsync(ParseResult result) =>
        Bind(result)?.Match(
            RunCommand.ExecuteAsync,
            config => Task.FromResult(ConfigCommand.Execute(config)),
            plugins => Task.FromResult(PluginsCommand.Execute(plugins)))
        ?? Task.FromResult(false);
}
