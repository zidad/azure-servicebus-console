using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
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

    public FakeNamespaceService Namespaces { get; } = new();
    public FakeQueueService Queues { get; } = new();
    public LiveCountsOptions LiveCounts { get; } = new();
    public FakeTopicService Topics { get; } = new();
    public FakeMessageService Messages { get; } = new();
    public NavigationState NavState { get; } = new();

    /// <summary>Per-test cache folder, so tests never touch the user's real cache.</summary>
    public string CacheDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "sb-console-tests", Guid.NewGuid().ToString("N"));
    public ConsoleNavigationManager Navigation { get; } = new();
    public ServiceBusConnection Connection { get; } = new();

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
                    services.AddSingleton<INamespaceService>(Namespaces);
                    services.AddSingleton<IQueueService>(Queues);
                    services.AddSingleton<ITopicService>(Topics);
                    services.AddSingleton<IMessageService>(Messages);
                    services.AddSingleton(NavState);
                    services.AddSingleton<MessageBrowserStore>();
                    services.AddSingleton(new FileCache(NullLogger<FileCache>.Instance, CacheDirectory));
                    services.AddSingleton<SubscriptionCatalog>();
                    services.AddSyntaxLanguages();
                    services.AddSingleton(LiveCounts);
                },
            },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Starts on the connection page (local emulator is preselected) and opens its queue list, the way a user does.</summary>
    public Task<HeadlessTerminal> OpenQueuesAsync() => OpenAsync(ConsoleKey.F1);

    /// <summary>Same as <see cref="OpenQueuesAsync"/>, for the topic list.</summary>
    public Task<HeadlessTerminal> OpenTopicsAsync() => OpenAsync(ConsoleKey.F2);

    // A key, not a click: the connection page can still shift its buttons (e.g. the subscription
    // spinner appears), so a click at a position read from an earlier frame may miss.
    private async Task<HeadlessTerminal> OpenAsync(ConsoleKey key)
    {
        var terminal = await StartAsync();
        await WaitForTableFocusAsync(terminal);
        var connectionFocus = terminal.FocusKey;
        await terminal.SendKeyAsync(key, '\0', cancellationToken: TestContext.Current.CancellationToken);
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
            _ => terminal.FocusKey != previousKey && FocusedAttribute(terminal, "data-focus-order") == "1",
            Timeout,
            cancellationToken: TestContext.Current.CancellationToken);

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
        var ct = TestContext.Current.CancellationToken;
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
