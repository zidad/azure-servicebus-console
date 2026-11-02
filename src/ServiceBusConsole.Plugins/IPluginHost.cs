namespace ServiceBusConsole.Plugins;

/// <summary>What the console does for plugins: files, processes, opening things. Tests replace it.</summary>
public interface IPluginHost
{
    /// <summary>azsb's state directory; plugins write under it through <see cref="PluginFiles"/>.</summary>
    string StateDirectory { get; }

    /// <summary>Runs a process to its end and collects its output; a cancel kills it.</summary>
    Task<ProcessResult> RunProcessAsync(ProcessSpec spec, CancellationToken ct);

    /// <summary>Opens a file or URL with its default application (macOS <c>open</c>, Linux <c>xdg-open</c>, Windows shell).</summary>
    Task OpenAsync(string pathOrUrl, CancellationToken ct);
}
