using System.Collections.Concurrent;

namespace ServiceBusConsole.Plugins.Claude.Tests;

/// <summary>Records processes instead of starting them; <see cref="Run"/> answers them. Its state directory is a fresh temp folder.</summary>
public sealed class FakePluginHost : IPluginHost, IDisposable
{
    public string StateDirectory { get; } = Path.Combine(Path.GetTempPath(), "azsb-claude-tests", Guid.NewGuid().ToString("N"));
    public ConcurrentQueue<ProcessSpec> Processes { get; } = new();
    public Func<ProcessSpec, CancellationToken, Task<ProcessResult>> Run { get; set; } =
        (_, _) => Task.FromResult(new ProcessResult(0, "", ""));

    public Task<ProcessResult> RunProcessAsync(ProcessSpec spec, CancellationToken ct)
    {
        Processes.Enqueue(spec);
        return Run(spec, ct);
    }

    public Task OpenAsync(string pathOrUrl, CancellationToken ct) => Task.CompletedTask;

    public void Dispose()
    {
        if (Directory.Exists(StateDirectory)) Directory.Delete(StateDirectory, recursive: true);
    }
}
