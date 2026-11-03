using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RazorConsole.Core.Focus;
using RazorConsole.Core.Rendering;
using RazorConsole.Testing;
using ServiceBusConsole.Components;

namespace ServiceBusConsole.Tests;

/// <summary>Mounts the real <see cref="App"/> with fake services, starting at a route.</summary>
public sealed class AppHarness
{
    // Generous so slow CI runners pass; a hang still fails.
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public AppHarness()
    {
        OpenNamespace = info => FakeNamespace.For(info, Services);
        Connection = new(new FakeNamespaceOpener(info => OpenNamespace(info)));
    }

    public FakeNamespaceService Namespaces { get; } = new();

    /// <summary>What the app lists subscriptions and namespaces with; <see cref="Namespaces"/> unless set, e.g. to a caching one.</summary>
    public INamespaceService? NamespaceService { get; set; }
    public FakeServices Services { get; } = new();
    public FakeQueueService Queues => Services.Queues;
    public LiveCountsOptions LiveCounts { get; } = new();
    public FakeTopicService Topics => Services.Topics;
    public FakeMessageService Messages => Services.Messages;
    public FakeMetricsService Metrics => Services.Metrics;

    /// <summary>The namespace a choice opens; by default like the real opener (<see cref="FakeNamespace.For"/>).</summary>
    public Func<NamespaceInfo, IServiceBusNamespace> OpenNamespace { get; set; }

    public MetricsOptions MetricsOptions { get; } = new();

    /// <summary>The installed plugins; none by default.</summary>
    public List<IPlugin> Plugins { get; } = [];

    /// <summary>Settings as <c>IConfiguration</c> paths, e.g. <c>plugins:fake:enabled</c>.</summary>
    public Dictionary<string, string?> Settings { get; } = [];

    public FakePluginHost PluginHost { get; } = new();
    public NavigationState NavState { get; } = new();

    /// <summary>Per-test cache folder, so tests never touch the user's real cache.</summary>
    public string CacheDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "sb-console-tests", Guid.NewGuid().ToString("N"));
    public ConsoleNavigationManager Navigation { get; } = new();
    public ServiceBusConnection Connection { get; }

    public Task<HeadlessTerminal> StartAsync(int width = 120, int height = 30)
    {
        return HeadlessTerminal.StartAsync<App>(
            new HeadlessTerminalOptions
            {
                Width = width,
                Height = height,
                ConfigureServices = services =>
                {
                    services.AddLogging();
                    services.AddSingleton<NavigationManager>(Navigation);
                    services.AddSingleton(Connection);
                    services.AddSingleton(NamespaceService ?? Namespaces);
                    services.AddSingleton(NavState);
                    services.AddSingleton<MessageBrowserStore>();
                    services.AddSingleton<ActivityLog>();
                    services.AddSingleton<SessionContext>();
                    services.AddSingleton(new FileCache(NullLogger<FileCache>.Instance, CacheDirectory));
                    services.AddSingleton<SubscriptionCatalog>();
                    services.AddSingleton<INamespaceOpener>(new FakeNamespaceOpener(info => OpenNamespace(info)));
                    services.AddSingleton<ExplorerTree>();
                    services.AddSyntaxLanguages();
                    services.AddSingleton(LiveCounts);
                    services.AddSingleton(MetricsOptions);
                    services.AddSingleton<MetricsVisibility>();
                    services.AddSingleton<MetricsRange>();
                    services.AddSingleton<IPluginHost>(PluginHost);
                    services.AddPlugins(new PluginCatalog(Plugins), new ConfigurationBuilder().AddInMemoryCollection(Settings).Build());
                },
            },
            Ct);
    }

    /// <summary>Starts on the connection page (local emulator is preselected) and opens its queue list, the way a user does.</summary>
    public Task<HeadlessTerminal> OpenQueuesAsync(int height = 30) => OpenAsync(ConsoleKey.F1, height);

    /// <summary>Same as <see cref="OpenQueuesAsync"/>, for the topic list.</summary>
    public Task<HeadlessTerminal> OpenTopicsAsync() => OpenAsync(ConsoleKey.F2);

    public static readonly AzureSubscription AzureSubscription = new("Contoso Development", "6f1d2c3b-0000-4000-8000-000000000001");

    public static readonly NamespaceInfo AzureNamespace = new()
    {
        Name = "sb-orders-dev", FullyQualifiedNamespace = "sb-orders-dev.servicebus.windows.net", ResourceGroup = "rg-messaging-dev",
        Location = "West Europe", Sku = "Standard", Status = "Active",
        ResourceId = "/subscriptions/6f1d2c3b-0000-4000-8000-000000000001/resourceGroups/rg-messaging-dev/providers/Microsoft.ServiceBus/namespaces/sb-orders-dev",
    };

    /// <summary>
    /// Starts on the connection page, picks <see cref="AzureSubscription"/> and its <paramref name="ns"/> (by default
    /// <see cref="AzureNamespace"/>), then opens the page of <paramref name="key"/> (F1 queues, F2 topics, F3 all subscriptions);
    /// tall enough for the metrics panel.
    /// </summary>
    public async Task<HeadlessTerminal> OpenAzureAsync(ConsoleKey key, int height = 40, NamespaceInfo? ns = null)
    {
        var chosen = ns ?? AzureNamespace;
        Namespaces.Subscriptions.Add(AzureSubscription);
        Namespaces.Namespaces.Add(chosen);
        var terminal = await StartAsync(height: height);
        await WaitForTableFocusAsync(terminal);
        await SelectAzureNamespaceAsync(terminal, chosen);
        var connectionFocus = terminal.FocusKey;
        await terminal.SendKeyAsync(key, '\0', cancellationToken: Ct);
        await WaitForTableFocusAsync(terminal, connectionFocus);
        return terminal;
    }

    /// <summary>
    /// On the connection page, moves down the tree from the emulator's namespace (selected at start) until <paramref name="ns"/>
    /// is the connection.
    /// </summary>
    public async Task SelectAzureNamespaceAsync(HeadlessTerminal terminal, NamespaceInfo ns)
    {
        var target = $"{Icons.Namespace} {ns.Name}";
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText(target) && !f.ContainsText("Discovering"), Timeout, cancellationToken: Ct);
        // One tree row per line.
        var lines = Enumerable.Range(0, frame.Height).Select(frame.GetLine).ToList();
        var rows = lines.FindIndex(l => l.Contains(target)) - lines.FindIndex(l => l.Contains($"{Icons.Namespace} localhost"));
        foreach (var _ in Enumerable.Range(0, rows)) await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => Connection.CurrentNamespace == ns.FullyQualifiedNamespace, Timeout, cancellationToken: Ct);
    }

    // A key, not a click: the connection page can still shift its buttons (e.g. the subscription
    // spinner appears), so a click at a position read from an earlier frame may miss.
    private async Task<HeadlessTerminal> OpenAsync(ConsoleKey key, int height = 30)
    {
        var terminal = await StartAsync(height: height);
        await WaitForTableFocusAsync(terminal);
        // The emulator's namespace loads in the background; the key does nothing until its button shows.
        await terminal.WaitForTextAsync($"[{key}]", Timeout, Ct);
        var connectionFocus = terminal.FocusKey;
        await terminal.SendKeyAsync(key, '\0', cancellationToken: Ct);
        await WaitForTableFocusAsync(terminal, connectionFocus);
        return terminal;
    }

    /// <summary>
    /// Waits until the page's table (focus order 1) has focus. Pages focus it after rendering,
    /// so keys sent as soon as the rows appear can still go to the previous element.
    /// </summary>
    /// <param name="terminal">The terminal.</param>
    /// <param name="previousKey">The focus key before navigating; the previous page's table also has focus order 1.</param>
    public static Task WaitForTableFocusAsync(HeadlessTerminal terminal, string? previousKey = null)
        => WaitForFocusAsync(terminal, focusOrder: "1", previousKey);

    /// <summary>
    /// Waits until the element with <paramref name="focusOrder"/> has focus. Dialogs and pages take focus
    /// one render after their text appears; a key sent before that goes to the element that had focus.
    /// </summary>
    public static Task WaitForFocusAsync(HeadlessTerminal terminal, string focusOrder, string? previousKey = null)
        => terminal.WaitUntilAsync(
            _ => terminal.FocusKey != previousKey && FocusedAttribute(terminal, "data-focus-order") == focusOrder,
            Timeout,
            cancellationToken: Ct);

    // HeadlessTerminal exposes only the focus key, so read the focused element's attributes from FocusManager.
    public static string? FocusedAttribute(HeadlessTerminal terminal, string name)
    {
        var focus = terminal.Services.GetRequiredService<FocusManager>();
        var field = typeof(FocusManager).GetField("_focusTargets", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(nameof(FocusManager), "_focusTargets");
        if (field.GetValue(focus) is not IEnumerable targets) return null;
        foreach (var target in targets.Cast<object>().ToList())
        {
            var type = target.GetType();
            if (type.GetProperty("Key")?.GetValue(target) as string != focus.CurrentFocusKey) continue;
            return type.GetProperty("Attributes")?.GetValue(target) is IReadOnlyDictionary<string, string?> attributes
                ? attributes.GetValueOrDefault(name)
                : null;
        }
        return null;
    }

    /// <summary>Waits for <paramref name="text"/> to appear, then clicks its first character.</summary>
    public static async Task ClickTextAsync(HeadlessTerminal terminal, string text, bool exactLine = false)
    {
        var ct = Ct;
        var frame = await terminal.WaitUntilAsync(f => Find(f, text, exactLine) is not null, Timeout, cancellationToken: ct);
        if (Find(frame, text, exactLine) is not var (x, y)) throw new InvalidOperationException($"'{text}' not on screen");
        await terminal.ClickAsync(x, y, ct);
    }

    private static (int X, int Y)? Find(TerminalFrame frame, string text, bool exactLine)
    {
        for (var y = 0; y < frame.Height; y++)
        {
            var line = frame.GetLine(y);
            var x = line.IndexOf(text, StringComparison.Ordinal);
            if (x < 0) continue;
            if (exactLine && line.Trim(' ', '│', '>') != text) continue;
            return (x, y);
        }
        return null;
    }
}
