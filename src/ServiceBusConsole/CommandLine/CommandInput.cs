namespace ServiceBusConsole.CommandLine;

/// <summary>What the command line asks for: <see cref="RunInput"/>, <see cref="ConfigInput"/>, <see cref="PluginsInput"/> or <see cref="NamespaceInput"/>.</summary>
public abstract record CommandInput
{
    private protected CommandInput() { }

    public TResult Match<TResult>(
        Func<RunInput, TResult> run, Func<ConfigInput, TResult> config, Func<PluginsInput, TResult> plugins, Func<NamespaceInput, TResult> ns) => this switch
    {
        RunInput r => run(r),
        ConfigInput c => config(c),
        PluginsInput p => plugins(p),
        NamespaceInput n => ns(n),
        _ => throw new InvalidOperationException($"Unknown command input {GetType().Name}."),
    };
}
