namespace ServiceBusConsole.Plugins.Linear.Tests;

/// <summary>Records reports as they are made, unlike <see cref="Progress{T}"/>, which posts them.</summary>
internal sealed class RecordingProgress : IProgress<string>
{
    private readonly List<string> _reports = [];

    public IReadOnlyList<string> Reports => _reports;

    public void Report(string value) => _reports.Add(value);
}
