using System.Collections.Concurrent;

namespace ServiceBusConsole.Plugins.Claude.Tests;

public sealed class RecordingProgress : IProgress<string>
{
    public ConcurrentQueue<string> Reports { get; } = new();

    public void Report(string value) => Reports.Enqueue(value);
}
