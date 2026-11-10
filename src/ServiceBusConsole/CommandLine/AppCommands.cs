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
        Description = CommandLineStrings.AppCommands_SubscriptionOption,
    };

    private readonly Option<string?> _namespace = new("--namespace", "-n")
    {
        Description = CommandLineStrings.AppCommands_NamespaceOption,
    };

    private readonly Option<string?> _open = new("--open", "-o")
    {
        Description = CommandLineStrings.AppCommands_OpenOption,
    };

    private readonly Option<string?> _theme = new("--theme", "-t")
    {
        Description = CommandLineStrings.AppCommands_ThemeOption,
    };

    private readonly Argument<ConfigAction> _action = new("action") { Description = CommandLineStrings.AppCommands_ActionArgument };
    private readonly Argument<string?> _key = new("key") { Description = CommandLineStrings.AppCommands_KeyArgument, Arity = ArgumentArity.ZeroOrOne };
    private readonly Argument<string?> _value = new("value") { Description = CommandLineStrings.AppCommands_ValueArgument, Arity = ArgumentArity.ZeroOrOne };
    private readonly Option<bool> _all = new("--all") { Description = CommandLineStrings.AppCommands_AllOption };
    private readonly Argument<string[]> _topic = new("command") { Description = CommandLineStrings.AppCommands_CommandArgument, Arity = ArgumentArity.ZeroOrMore };

    private readonly Command _run;
    private readonly Command _config;
    private readonly Command _pluginsCommand;

    public AppCommands(IHostBuilder host) : this(host, PluginCatalog.Empty) { }

    public AppCommands(IHostBuilder host, PluginCatalog plugins)
    {
        _host = host;
        _plugins = plugins;
        _run = new("run", CommandLineStrings.AppCommands_Run) { _subscription, _namespace, _open, _theme };
        _config = new("config", CommandLineStrings.AppCommands_Config) { _action, _key, _value, _all };
        _pluginsCommand = new("plugins", CommandLineStrings.AppCommands_Plugins);
        var help = new Command("help", CommandLineStrings.AppCommands_Help) { _topic };

        // Named azsb, not after the executable: the tool's shim runs ServiceBusConsole.
        Root = new("azsb", CommandLineStrings.AppCommands_Root)
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
