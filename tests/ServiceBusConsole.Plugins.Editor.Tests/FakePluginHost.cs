using System.Collections.Concurrent;

namespace ServiceBusConsole.Plugins.Editor.Tests;

/// <summary>Records processes instead of starting them, in its own temp state directory.</summary>
public sealed class FakePluginHost : IPluginHost, IDisposable
{
    public string StateDirectory { get; } = Path.Combine(Path.GetTempPath(), "azsb-tests", Guid.NewGuid().ToString("N"));
    public ConcurrentQueue<ProcessSpec> Processes { get; } = new();
    public Func<ProcessSpec, ProcessResult> Run { get; init; } = _ => new ProcessResult(0, "", "");

    public Task<ProcessResult> RunProcessAsync(ProcessSpec spec, CancellationToken ct)
    {
        Processes.Enqueue(spec);
        return Task.FromResult(Run(spec));
    }

    public Task OpenAsync(string pathOrUrl, CancellationToken ct) => Task.CompletedTask;

    public void Dispose()
    {
        if (Directory.Exists(StateDirectory)) Directory.Delete(StateDirectory, recursive: true);
    }
}
