namespace ServiceBusConsole.Plugins;

/// <summary>A process to run: each argument is passed as is, with no shell quoting.</summary>
public sealed record ProcessSpec(string FileName, IReadOnlyList<string> Arguments)
{
    public string? WorkingDirectory { get; init; }

    /// <summary>Written to the process's standard input, which is then closed.</summary>
    public string? StandardInput { get; init; }

    /// <summary>Variables set for the process; a null value removes one.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; init; } = new Dictionary<string, string?>();
}
