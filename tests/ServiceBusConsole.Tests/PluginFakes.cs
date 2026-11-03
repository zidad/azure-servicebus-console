using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: ServiceBusConsole.Plugins.ServiceBusConsolePlugin(typeof(ServiceBusConsole.Tests.FakePlugin))]

namespace ServiceBusConsole.Tests;

/// <summary>A plugin whose actions are given; parameterless, so attribute discovery can create it.</summary>
public sealed class FakePlugin : IPlugin
{
    public string Id { get; init; } = "fake";
    public string Name { get; init; } = "Fake";
    public string Description { get; init; } = "Does fake things with messages";
    public IReadOnlyList<PluginSetting> Settings { get; init; } =
    [
        new("repo", "Repository to file issues in", "zidad/example"),
        new("token", "API token", IsSecret: true),
    ];
    public IReadOnlyList<IMessageAction> Actions { get; init; } = [];
    public Func<IConfiguration, PluginEnvironment, string?> Missing { get; init; } = (_, _) => null;

    string? IPlugin.Missing(IConfiguration settings, PluginEnvironment environment) => Missing(settings, environment);

    public void ConfigureServices(IServiceCollection services)
    {
        foreach (var action in Actions) services.AddSingleton(action);
    }
}

/// <summary>Runs <see cref="Run"/>; records the contexts it ran with.</summary>
public sealed class FakeMessageAction(string pluginId, string label, Func<MessageActionContext, CancellationToken, Task<ActionResult>> run) : IMessageAction
{
    public string PluginId { get; } = pluginId;
    public string Id { get; init; } = label.ToLowerInvariant().Replace(' ', '-');
    public string Label { get; } = label;
    public string Icon { get; init; } = "🐙";
    public Func<MessageActionContext, bool> When { get; init; } = _ => true;
    public Func<MessageActionContext, CancellationToken, Task<ActionResult>> Run { get; } = run;
    public ConcurrentQueue<MessageActionContext> Contexts { get; } = new();

    public bool CanRun(MessageActionContext context) => When(context);

    public Task<ActionResult> RunAsync(MessageActionContext context, CancellationToken ct)
    {
        Contexts.Enqueue(context);
        return Run(context, ct);
    }
}

/// <summary>Records processes and opened paths instead of starting anything.</summary>
public sealed class FakePluginHost : IPluginHost
{
    public string StateDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "azsb-tests", Guid.NewGuid().ToString("N"));
    public ConcurrentQueue<ProcessSpec> Processes { get; } = new();
    public ConcurrentQueue<string> Opened { get; } = new();
    public ProcessResult Result { get; set; } = new(0, "", "");

    public Task<ProcessResult> RunProcessAsync(ProcessSpec spec, CancellationToken ct)
    {
        Processes.Enqueue(spec);
        return Task.FromResult(Result);
    }

    public Task OpenAsync(string pathOrUrl, CancellationToken ct)
    {
        Opened.Enqueue(pathOrUrl);
        return Task.CompletedTask;
    }
}
