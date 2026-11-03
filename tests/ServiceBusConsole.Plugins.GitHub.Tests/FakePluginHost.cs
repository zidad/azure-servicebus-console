using System.Collections.Concurrent;

namespace ServiceBusConsole.Plugins.GitHub.Tests;

/// <summary>Records processes, and the body file each one was given, instead of starting anything.</summary>
public sealed class FakePluginHost : IPluginHost
{
    public string StateDirectory { get; } = Path.Combine(Path.GetTempPath(), "azsb-tests", Guid.NewGuid().ToString("N"));
    public ConcurrentQueue<ProcessSpec> Processes { get; } = new();
    public ConcurrentQueue<string> BodyFiles { get; } = new();
    public ProcessResult Result { get; init; } = new(0, "https://github.com/zidad/example/issues/12\n", "");
    public Exception? StartFailure { get; init; }

    public Task<ProcessResult> RunProcessAsync(ProcessSpec spec, CancellationToken ct)
    {
        if (StartFailure is { } failure) return Task.FromException<ProcessResult>(failure);
        Processes.Enqueue(spec);
        if (spec.Arguments.SkipWhile(a => a != "--body-file").Skip(1).FirstOrDefault() is { } path) BodyFiles.Enqueue(File.ReadAllText(path));
        return Task.FromResult(Result);
    }

    public Task OpenAsync(string pathOrUrl, CancellationToken ct) => Task.CompletedTask;
}
