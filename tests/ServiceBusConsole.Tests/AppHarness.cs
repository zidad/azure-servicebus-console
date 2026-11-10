using System.Collections;
using System.Diagnostics;
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

    /// <summary>What the app sent to the terminal's clipboard (OSC 52 sequences), in order.</summary>
    public System.Collections.Concurrent.ConcurrentQueue<string> Copied { get; } = [];

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

    /// <summary>The app's clock for recently opened namespaces.</summary>
    public TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>In <see cref="CacheDirectory"/>; created on first use, so set that first.</summary>
    public FileCache Cache => field ??= new(NullLogger<FileCache>.Instance, CacheDirectory);

    /// <summary>The recently opened namespaces; record some before starting to show them.</summary>
    public RecentNamespaces Recent => field ??= new(Cache, Time);
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
                    services.AddSingleton(new Clipboard { Write = Copied.Enqueue });
                    services.AddSingleton<SessionContext>();
                    services.AddSingleton(Cache);
                    services.AddSingleton(Recent);
                    services.AddSingleton<NamespaceSwitcher>();
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
        // One tree row per line; the recent namespaces above the subscriptions may list both again.
        var lines = Enumerable.Range(0, frame.Height).Select(frame.GetLine).ToList();
        var start = lines.FindLastIndex(l => l.Contains($"{Icons.Namespace} localhost"));
        var rows = lines.FindIndex(start, l => l.Contains(target)) - start;
        foreach (var _ in Enumerable.Range(0, rows)) await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => Connection.CurrentNamespace == ns.FullyQualifiedNamespace, Timeout, cancellationToken: Ct);
    }

    /// <summary>The connection page's footer button for a section's F-key: its key chip and label.</summary>
    public static string SectionButton(ConsoleKey key) => key switch
    {
        ConsoleKey.F1 => $"1  {Icons.Queue} Queues",
        ConsoleKey.F2 => $"2  {Icons.Topic} Topics",
        _ => $"3  {Icons.Subscription} All Subscriptions",
    };

    // A key, not a click: the connection page can still shift its buttons (e.g. the subscription
    // spinner appears), so a click at a position read from an earlier frame may miss.
    private async Task<HeadlessTerminal> OpenAsync(ConsoleKey key, int height = 30)
    {
        var terminal = await StartAsync(height: height);
        await WaitForTableFocusAsync(terminal);
        // The emulator's namespace loads in the background; the key does nothing until its button shows.
        await terminal.WaitForTextAsync(SectionButton(key), Timeout, Ct);
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
        => terminal.WaitUntilAsync(
            // Until the previous page's table unregisters, its stale focus would pass.
            _ => terminal.FocusKey != previousKey && FocusTargets(terminal) is var targets
                && targets.Count(t => t.Attributes.GetValueOrDefault("data-focus-order") == "1") == 1
                && targets.Any(t => t.Key == terminal.FocusKey && t.Attributes.GetValueOrDefault("data-focus-order") == "1"),
            Timeout,
            cancellationToken: Ct);

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
        => FocusTargets(terminal).FirstOrDefault(t => t.Key == terminal.FocusKey)?.Attributes.GetValueOrDefault(name);

    private static IReadOnlyList<FocusTarget> FocusTargets(HeadlessTerminal terminal)
    {
        var focus = terminal.Services.GetRequiredService<FocusManager>();
        var field = typeof(FocusManager).GetField("_focusTargets", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(nameof(FocusManager), "_focusTargets");
        if (field.GetValue(focus) is not IEnumerable targets) return [];
        return [.. targets.Cast<object>().ToList().Select(target => target.GetType() is var type
                && type.GetProperty("Key")?.GetValue(target) is string key
                && type.GetProperty("Attributes")?.GetValue(target) is IReadOnlyDictionary<string, string?> attributes
                ? new FocusTarget(key, attributes)
                : null).OfType<FocusTarget>()];
    }

    private sealed record FocusTarget(string Key, IReadOnlyDictionary<string, string?> Attributes);

    /// <summary>Waits for <paramref name="text"/> to appear, then clicks its first character.</summary>
    public static Task ClickTextAsync(HeadlessTerminal terminal, string text, bool exactLine = false) =>
        ClickSettledAsync(terminal, f => Find(f, text, exactLine));

    /// <summary>
    /// Clicks where <paramref name="locate"/> points once it has pointed at the same cell for <see cref="SettleTime"/>:
    /// rows that load in the background move what a click would hit.
    /// </summary>
    public static async Task ClickSettledAsync(HeadlessTerminal terminal, Func<TerminalFrame, (int X, int Y)?> locate)
    {
        var ct = Ct;
        var limit = Stopwatch.StartNew();
        var still = Stopwatch.StartNew();
        (int X, int Y)? last = null;
        while (limit.Elapsed < Timeout)
        {
            var at = locate(terminal.CurrentFrame);
            if (at != last)
            {
                last = at;
                still.Restart();
            }
            else if (at is var (x, y) && still.Elapsed >= SettleTime)
            {
                await terminal.ClickAsync(x, y, ct);
                return;
            }
            await Task.Delay(10, ct);
        }
        throw new TimeoutException($"No settled click target within {Timeout}; last at {last?.ToString() ?? "none"}.{Environment.NewLine}{terminal.CurrentFrame.ScreenText}");
    }

    /// <summary>
    /// Waits until <paramref name="predicate"/> holds and neither the screen nor the focus has changed for <see cref="SettleTime"/>:
    /// before keys to a page that is still reloading, where a key can land on the list being replaced.
    /// </summary>
    public static Task<HeadlessTerminalFrame> WaitForSettledAsync(HeadlessTerminal terminal, Func<TerminalFrame, bool> predicate)
    {
        var still = Stopwatch.StartNew();
        (string Screen, string? Focus)? last = null;
        return terminal.WaitUntilAsync(frame =>
        {
            (string, string?)? now = predicate(frame) ? (frame.ScreenText, terminal.FocusKey) : null;
            if (now is null || now != last)
            {
                last = now;
                still.Restart();
                return false;
            }
            return still.Elapsed >= SettleTime;
        }, Timeout, cancellationToken: Ct);
    }

    public static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(250);

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
