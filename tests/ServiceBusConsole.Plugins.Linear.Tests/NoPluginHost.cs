namespace ServiceBusConsole.Plugins.Linear.Tests;

/// <summary>The Linear plugin runs no processes and writes no files.</summary>
internal sealed class NoPluginHost : IPluginHost
{
    public string StateDirectory => throw new NotSupportedException();

    public Task<ProcessResult> RunProcessAsync(ProcessSpec spec, CancellationToken ct) => throw new NotSupportedException();

    public Task OpenAsync(string pathOrUrl, CancellationToken ct) => throw new NotSupportedException();
}
