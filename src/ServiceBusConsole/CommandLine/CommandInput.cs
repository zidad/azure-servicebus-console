namespace ServiceBusConsole.CommandLine;

/// <summary>What the command line asks for: <see cref="RunInput"/>, <see cref="ConfigInput"/> or <see cref="PluginsInput"/>.</summary>
public abstract record CommandInput
{
    private protected CommandInput() { }

    public TResult Match<TResult>(Func<RunInput, TResult> run, Func<ConfigInput, TResult> config, Func<PluginsInput, TResult> plugins) => this switch
    {
        RunInput r => run(r),
        ConfigInput c => config(c),
        PluginsInput p => plugins(p),
        _ => throw new InvalidOperationException($"Unknown command input {GetType().Name}."),
    };
}
