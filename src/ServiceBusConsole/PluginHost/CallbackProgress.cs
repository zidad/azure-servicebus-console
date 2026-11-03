namespace ServiceBusConsole.PluginHost;

/// <summary>Reports on the reporting thread, in order; unlike <see cref="Progress{T}"/>, which posts to the thread pool.</summary>
public sealed class CallbackProgress(Action<string> report) : IProgress<string>
{
    public static CallbackProgress None { get; } = new(_ => { });

    public void Report(string value) => report(value);
}
