namespace ServiceBusConsole.CommandLine;

/// <summary>A command that works on one namespace: check, list, peek, retry, delete, purge or send.</summary>
public sealed record NamespaceInput : CommandInput
{
    public required NamespaceCommand Command { get; init; }

    public NamespaceTarget Target { get; init; } = new();

    /// <summary><c>--json</c>: machine-readable output on standard output.</summary>
    public bool Json { get; init; }

    /// <summary><c>--confirm</c>: the namespace's name, which a protected namespace (<c>cli.protect</c>) needs for any change.</summary>
    public string? Confirm { get; init; }

    public required Func<CommandServices> Services { get; init; }
}
