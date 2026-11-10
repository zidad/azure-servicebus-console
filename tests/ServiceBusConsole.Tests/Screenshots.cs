using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RazorConsole.Testing;
using Spectre.Console;

namespace ServiceBusConsole.Tests;

/// <summary>
/// Dumps every page with sample data as cell JSON (text + style) for design review.
/// Runs only when SCREENSHOT_DIR is set; tools/update-screenshots.sh renders them into docs/screenshots.
/// </summary>
[NotInParallel(nameof(Screenshots))]
public sealed class Screenshots
{
    [Test]
    public async Task AllPages()
    {
        if (Environment.GetEnvironmentVariable("SCREENSHOT_DIR") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir);
        using var theme = UseScreenshotTheme();

        var app = SampleApp();
        app.LiveCounts.Interval = TimeSpan.FromMilliseconds(200);
        await SeedRecentAsync(app);
        await using var terminal = await app.StartAsync(width: 120, height: 36);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await app.SelectAzureNamespaceAsync(terminal, AppHarness.AzureNamespace);
        var tree = terminal.Services.GetRequiredService<ExplorerTree>();
        foreach (var name in new[] { "Contoso Production", "Contoso Sandbox" })
            tree.SetOpen(new ExplorerNode.Subscription(app.Namespaces.Subscriptions.Single(s => s.Name == name)), false);
        await terminal.WaitUntilAsync(f => f.ContainsText("▸ 🔑 Contoso Sandbox"), AppHarness.Timeout, cancellationToken: Ct);
        await Save(terminal, dir, "connection");

        var connectionFocus = terminal.FocusKey;
        await AppHarness.ClickTextAsync(terminal, "1  📬 Queues");
        await AppHarness.WaitForTableFocusAsync(terminal, connectionFocus);
        await terminal.WaitForTextAsync("Queues (5)", AppHarness.Timeout, Ct);
        await WaitForChartsAsync(terminal, "📬 queue payments");
        await Save(terminal, dir, "queues");

        app.Queues.Update("emails", q => q with { ActiveMessageCount = 12 });
        await terminal.WaitUntilAsync(f => f.ContainsText(" 12 "), AppHarness.Timeout, cancellationToken: Ct);
        await Save(terminal, dir, "queues-live");

        await AppHarness.ClickTextAsync(terminal, "sb-orders-dev");
        await terminal.WaitForTextAsync("Go to", AppHarness.Timeout, Ct);
        await terminal.WaitForTextAsync("sb-orders-prd", AppHarness.Timeout, Ct);
        await Save(terminal, dir, "namespace-dropdown");
        await Key(terminal, ConsoleKey.Escape, '\u001b');
        await terminal.WaitUntilAsync(f => !f.ContainsText("Go to"), AppHarness.Timeout, cancellationToken: Ct);

        await AppHarness.ClickTextAsync(terminal, "Queues (5)");
        await terminal.WaitForTextAsync("Go to", AppHarness.Timeout, Ct);
        await Save(terminal, dir, "section-dropdown");
        await Key(terminal, ConsoleKey.Escape, '\u001b');
        await terminal.WaitUntilAsync(f => !f.ContainsText("Go to"), AppHarness.Timeout, cancellationToken: Ct);

        await AppHarness.WaitForTableFocusAsync(terminal);
        var queuesTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Oem1, ':', ConsoleModifiers.Shift, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "100", queuesTable);
        await terminal.TypeAsync("pay", Ct);
        await terminal.WaitForTextAsync("📬 payments", AppHarness.Timeout, Ct);
        await Save(terminal, dir, "jump");
        await Key(terminal, ConsoleKey.Escape, '\u001b');
        await terminal.WaitUntilAsync(f => !f.ContainsText("Jump to"), AppHarness.Timeout, cancellationToken: Ct);

        await AppHarness.WaitForTableFocusAsync(terminal);
        await Key(terminal, ConsoleKey.F9);
        await terminal.WaitForTextAsync("Delete queue", AppHarness.Timeout, Ct);
        await Save(terminal, dir, "confirm-delete");
        await Key(terminal, ConsoleKey.N, 'n');
        await terminal.WaitUntilAsync(f => !f.ContainsText("Delete queue"), AppHarness.Timeout, cancellationToken: Ct);

        var queueTable = terminal.FocusKey;
        await Key(terminal, ConsoleKey.Enter, '\r');
        await terminal.WaitForTextAsync("payment-failed", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queueTable);
        await Key(terminal, ConsoleKey.Spacebar, ' ');
        await Key(terminal, ConsoleKey.DownArrow);
        await Key(terminal, ConsoleKey.Spacebar, ' ');
        await terminal.WaitForTextAsync("2 selected", AppHarness.Timeout, Ct);
        await Save(terminal, dir, "message-browser");

        await Key(terminal, ConsoleKey.F10);
        await terminal.WaitForTextAsync("move in one transaction", AppHarness.Timeout, Ct);
        await Save(terminal, dir, "retry-confirm");
        await Key(terminal, ConsoleKey.N, 'n');
        await terminal.WaitUntilAsync(f => !f.ContainsText("move in one transaction"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);

        await Key(terminal, ConsoleKey.Enter, '\r');
        await terminal.WaitForTextAsync("Message #", AppHarness.Timeout, Ct);
        await Save(terminal, dir, "message-detail");
        await Key(terminal, ConsoleKey.Escape, '\u001b');
        await terminal.WaitForTextAsync("2 selected", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);

        // Retry the two selected messages, holding the transaction so the progress dialog can be captured.
        app.Services.Transactions.Hold = new TaskCompletionSource();
        await Key(terminal, ConsoleKey.F10);
        await terminal.WaitForTextAsync("move in one transaction", AppHarness.Timeout, Ct);
        await Key(terminal, ConsoleKey.Y, 'y');
        await terminal.WaitForTextAsync("Retrying 2 messages", AppHarness.Timeout, Ct);
        await Save(terminal, dir, "retry-progress");
        app.Services.Transactions.Hold.SetResult();
        await terminal.WaitForTextAsync("Retried 2 of 2.", AppHarness.Timeout, Ct);
        await Save(terminal, dir, "retry-result");
        await AppHarness.ClickTextAsync(terminal, "⏎  OK");
        await terminal.WaitUntilAsync(f => !f.ContainsText("Retrying 2 messages") && !f.ContainsText("selected"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await Key(terminal, ConsoleKey.Escape, '\u001b');
        await terminal.WaitForTextAsync("Queues (5)", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await Key(terminal, ConsoleKey.Escape, '\u001b');
        await terminal.WaitUntilAsync(f => f.ContainsText("2  📢 Topics") && f.ContainsText("sb-orders-prd"), AppHarness.Timeout, cancellationToken: Ct);

        connectionFocus = terminal.FocusKey;
        await AppHarness.ClickTextAsync(terminal, "2  📢 Topics");
        await AppHarness.WaitForTableFocusAsync(terminal, connectionFocus);
        await terminal.WaitForTextAsync("Topics (3)", AppHarness.Timeout, Ct);
        await WaitForChartsAsync(terminal, "📢 topic order-events");
        await Save(terminal, dir, "topics");

        var topicTable = terminal.FocusKey;
        await Key(terminal, ConsoleKey.Enter, '\r');
        await terminal.WaitForTextAsync("shipping", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, topicTable);
        await WaitForChartsAsync(terminal, "📢 topic order-events");
        await Save(terminal, dir, "topic-subscriptions");
        await Key(terminal, ConsoleKey.Escape, '\u001b');
        await terminal.WaitForTextAsync("Topics (3)", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await Key(terminal, ConsoleKey.Escape, '\u001b');
        await terminal.WaitUntilAsync(f => f.ContainsText("3  📥 All Subscriptions") && f.ContainsText("sb-orders-prd"), AppHarness.Timeout, cancellationToken: Ct);

        connectionFocus = terminal.FocusKey;
        await AppHarness.ClickTextAsync(terminal, "3  📥 All Subscriptions");
        await AppHarness.WaitForTableFocusAsync(terminal, connectionFocus);
        await terminal.WaitForTextAsync("All Subscriptions (5)", AppHarness.Timeout, Ct);
        await WaitForChartsAsync(terminal, "📢 topic order-events");
        await Save(terminal, dir, "all-subscriptions");
    }

    [Test]
    public async Task PeekLock()
    {
        if (Environment.GetEnvironmentVariable("SCREENSHOT_DIR") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir);
        using var theme = UseScreenshotTheme();

        var app = SampleApp();
        var t0 = new DateTimeOffset(2026, 10, 26, 9, 14, 0, TimeSpan.Zero);
        app.Messages.Messages.Clear();
        app.Messages.Messages.AddRange(Enumerable.Range(1, 10).Select(i => Message(
            2000 + i, i % 3 == 1 ? "order-updated" : "order-created", $"{{\"orderId\":\"A-{2000 + i}\"}}", t0.AddMinutes(i), i % 4 == 3 ? 2 : 1, "") with { DeadLetterReason = null }));
        await using var terminal = await app.StartAsync(width: 120, height: 36);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await app.SelectAzureNamespaceAsync(terminal, AppHarness.AzureNamespace);
        var connectionFocus = terminal.FocusKey;
        await AppHarness.ClickTextAsync(terminal, "1  📬 Queues");
        await AppHarness.WaitForTableFocusAsync(terminal, connectionFocus);
        await terminal.WaitForTextAsync("Queues (5)", AppHarness.Timeout, Ct);

        await AppHarness.WaitForTableFocusAsync(terminal);
        var queuesTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Oem1, ':', ConsoleModifiers.Shift, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "100", queuesTable);
        await terminal.TypeAsync("orders", Ct);
        await terminal.WaitForTextAsync("📬 orders", AppHarness.Timeout, Ct);
        await Key(terminal, ConsoleKey.Enter, '\r');
        await terminal.WaitForTextAsync("order-created", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await Key(terminal, ConsoleKey.D1, '1');
        await AppHarness.WaitForSettledAsync(terminal, f => f.ContainsText("2  💀 DLQ") && f.ContainsText("order-created"));
        await AppHarness.WaitForTableFocusAsync(terminal);

        var table = terminal.FocusKey;
        await Key(terminal, ConsoleKey.R, 'r');
        await AppHarness.WaitForFocusAsync(terminal, "100", table);
        await Key(terminal, ConsoleKey.Enter, '\r');
        await AppHarness.WaitForSettledAsync(terminal, f => f.ContainsText("10 received") && f.ContainsText("10 held"));
        await AppHarness.WaitForTableFocusAsync(terminal);

        foreach (var _ in Enumerable.Range(0, 6)) await Key(terminal, ConsoleKey.DownArrow);
        await Key(terminal, ConsoleKey.A, 'a');
        await WaitForStatusAsync(terminal, "Abandoned message #2007");
        await Key(terminal, ConsoleKey.UpArrow);
        await Key(terminal, ConsoleKey.Spacebar, ' ');
        await Key(terminal, ConsoleKey.UpArrow);
        await Key(terminal, ConsoleKey.Spacebar, ' ');
        await terminal.WaitForTextAsync("2 selected", AppHarness.Timeout, Ct);
        await Key(terminal, ConsoleKey.C, 'c');
        await terminal.WaitForTextAsync("Complete 2 messages?", AppHarness.Timeout, Ct);
        await Key(terminal, ConsoleKey.Y, 'y');
        await WaitForStatusAsync(terminal, "Completed 2 messages");

        foreach (var _ in Enumerable.Range(0, 3)) await Key(terminal, ConsoleKey.UpArrow);
        foreach (var _ in Enumerable.Range(0, 3))
        {
            await Key(terminal, ConsoleKey.Spacebar, ' ');
            await Key(terminal, ConsoleKey.DownArrow);
        }
        await Key(terminal, ConsoleKey.UpArrow);
        await Key(terminal, ConsoleKey.UpArrow);
        await AppHarness.WaitForSettledAsync(terminal, f => f.ContainsText("3 selected"));
        await Save(terminal, dir, "peeklock");
    }

    private static async Task WaitForStatusAsync(HeadlessTerminal terminal, string status)
    {
        await terminal.WaitForTextAsync(status, AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
    }

    [Test]
    public async Task MetricsPanel()
    {
        if (Environment.GetEnvironmentVariable("SCREENSHOT_DIR") is not { Length: > 0 } dir) return;
        Directory.CreateDirectory(dir);
        using var theme = UseScreenshotTheme();

        var app = SampleApp();
        await using var terminal = await app.StartAsync(width: 120, height: 50);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.WaitUntilAsync(f => f.ContainsText("Contoso Development") && f.ContainsText("available for localhost"), AppHarness.Timeout, cancellationToken: Ct);
        await Save(terminal, dir, "connection-emulator");

        await app.SelectAzureNamespaceAsync(terminal, AppHarness.AzureNamespace);
        await WaitForChartsAsync(terminal, "🌐 namespace sb-orders-dev");
        await Save(terminal, dir, "connection-metrics");

        var connectionFocus = terminal.FocusKey;
        await Key(terminal, ConsoleKey.F1);
        await AppHarness.WaitForTableFocusAsync(terminal, connectionFocus);
        await WaitForChartsAsync(terminal, "📬 queue payments");
        await Key(terminal, ConsoleKey.OemMinus, '-');
        await Key(terminal, ConsoleKey.OemMinus, '-');
        await terminal.WaitUntilAsync(f => f.ContainsText("last 7 d") && f.ContainsText("-7d"), AppHarness.Timeout, cancellationToken: Ct);
        await Save(terminal, dir, "queues-7d");

        for (var i = 0; i < 6; i++) await Key(terminal, ConsoleKey.OemPlus, '+');
        await terminal.WaitUntilAsync(f => f.ContainsText("last 1 h") && f.ContainsText("-60m"), AppHarness.Timeout, cancellationToken: Ct);
        await Save(terminal, dir, "queues-1h");
    }

    // Opened the emulator yesterday and sb-orders-dev two hours ago.
    private static async Task SeedRecentAsync(AppHarness app)
    {
        var now = DateTimeOffset.UtcNow;
        var time = new FixedTime(now.AddDays(-1));
        app.Time = time;
        await app.Recent.RecordAsync(AzureSubscription.Emulator, NamespaceInfo.Emulator);
        time.Now = now.AddHours(-2);
        await app.Recent.RecordAsync(AppHarness.AzureSubscription, AppHarness.AzureNamespace);
        time.Now = now;
    }

    private static AppHarness SampleApp()
    {
        // Shown on the connection page; keep the real home folder out of published screenshots.
        var app = new AppHarness { CacheDirectory = "/home/you/.local/share/sb-console" };
        app.MetricsOptions.Debounce = TimeSpan.FromMilliseconds(50);
        app.Namespaces.Subscriptions.AddRange(
        [
            new AzureSubscription("Contoso Production", "6f1d2c3b-0000-4000-8000-000000000002"),
            new AzureSubscription("Contoso Development", "6f1d2c3b-0000-4000-8000-000000000001"),
            new AzureSubscription("Contoso Sandbox", "6f1d2c3b-0000-4000-8000-000000000003"),
        ]);
        app.Namespaces.Namespaces.AddRange(
        [
            new NamespaceInfo { Name = "sb-orders-dev", FullyQualifiedNamespace = "sb-orders-dev.servicebus.windows.net", ResourceGroup = "rg-messaging-dev", Location = "West Europe", Sku = "Standard", Status = "Active" },
            new NamespaceInfo { Name = "sb-orders-prd", FullyQualifiedNamespace = "sb-orders-prd.servicebus.windows.net", ResourceGroup = "rg-messaging-prd", Location = "West Europe", Sku = "Premium", Status = "Active" },
            new NamespaceInfo { Name = "sb-legacy", FullyQualifiedNamespace = "sb-legacy.servicebus.windows.net", ResourceGroup = "rg-legacy", Location = "North Europe", Sku = "Basic", Status = "Disabled" },
        ]);
        app.Queues.Queues.AddRange(
        [
            new QueueInfo { Name = "payments", ActiveMessageCount = 0, DeadLetterMessageCount = 3 },
            new QueueInfo { Name = "orders", ActiveMessageCount = 1284, ScheduledMessageCount = 12 },
            new QueueInfo { Name = "emails", ActiveMessageCount = 7, ScheduledMessageCount = 40 },
            new QueueInfo { Name = "audit-log", TransferMessageCount = 2 },
            new QueueInfo { Name = "invoices" },
        ]);
        app.Metrics.TrafficEntities.AddRange(["payments", "orders", "emails", "audit-log", "order-events", "notifications"]);
        app.Topics.Topics.AddRange(
        [
            new TopicInfo { Name = "order-events", SubscriptionCount = 3 },
            new TopicInfo { Name = "notifications", SubscriptionCount = 1, ScheduledMessageCount = 2 },
            new TopicInfo { Name = "price-updates", SubscriptionCount = 0 },
        ]);
        app.Topics.Subscriptions.AddRange(
        [
            new SubscriptionInfo { TopicName = "order-events", Name = "billing", ActiveMessageCount = 5 },
            new SubscriptionInfo { TopicName = "order-events", Name = "shipping", DeadLetterMessageCount = 2 },
            new SubscriptionInfo { TopicName = "order-events", Name = "analytics" },
            new SubscriptionInfo { TopicName = "notifications", Name = "push", ActiveMessageCount = 1, TransferMessageCount = 1 },
            new SubscriptionInfo { TopicName = "notifications", Name = "sms" },
        ]);
        var t0 = new DateTimeOffset(2026, 10, 26, 9, 14, 0, TimeSpan.Zero);
        app.Messages.Messages.AddRange(
        [
            Message(101, "payment-failed", "{\"orderId\":\"A-1001\",\"amount\":49.95}", t0, 10, "MaxDeliveryCountExceeded"),
            Message(102, "payment-failed", "{\"orderId\":\"A-1002\",\"amount\":12.50}", t0.AddMinutes(3), 10, "MaxDeliveryCountExceeded"),
            Message(117, "refund-requested", "{\"orderId\":\"A-0977\"}", t0.AddMinutes(41), 1, "InvalidPayload"),
        ]);
        return app;
    }

    private static MessageInfo Message(long sequence, string subject, string body, DateTimeOffset enqueued, int deliveries, string reason) => new()
    {
        SequenceNumber = sequence,
        State = MessageState.Peeked,
        MessageId = $"{Guid.CreateVersion7(enqueued):N}"[..20],
        Subject = subject,
        ContentType = "application/json",
        Body = Encoding.UTF8.GetBytes(body),
        DeliveryCount = deliveries,
        EnqueuedTime = enqueued,
        DeadLetterReason = reason,
        DeadLetterErrorDescription = reason == "MaxDeliveryCountExceeded" ? $"Message could not be consumed after {deliveries} delivery attempts." : null,
        Properties = [KeyValuePair.Create("source", "checkout-api")],
    };

    private static Task WaitForChartsAsync(HeadlessTerminal terminal, string label)
        => terminal.WaitUntilAsync(f => f.ContainsText(label) && f.ContainsText("now") && !f.ContainsText("Loading metrics"), AppHarness.Timeout, cancellationToken: Ct);

    private static Task Key(HeadlessTerminal terminal, ConsoleKey key, char ch = '\0')
        => terminal.SendKeyAsync(key, ch, cancellationToken: Ct);

    private static async Task Save(HeadlessTerminal terminal, string dir, string name)
    {
        // Let spinners and focus styling settle.
        await Task.Delay(300, Ct);
        var frame = terminal.CurrentFrame;
        var rows = new List<List<object?[]>>();
        for (var y = 0; y < frame.Height; y++)
        {
            var row = new List<object?[]>();
            for (var x = 0; x < frame.Width; x++)
            {
                var cell = frame[x, y];
                var style = cell.Style;
                row.Add([cell.Text, Hex(style?.Foreground), Hex(style?.Background), (int)(style?.Decoration ?? Decoration.None)]);
            }
            rows.Add(row);
        }
        var theme = Theme.Palette == Palette.Light ? "light" : "dark";
        await File.WriteAllTextAsync(Path.Combine(dir, $"{name}.json"), JsonSerializer.Serialize(new { frame.Width, frame.Height, Theme = theme, rows }), Ct);
    }

    // The terminal's 16 palette colors by name, which the renderer resolves with its color scheme; others as hex.
    private static readonly string[] PaletteNames =
        ["black", "maroon", "green", "olive", "navy", "purple", "teal", "silver", "grey", "red", "lime", "yellow", "blue", "fuchsia", "aqua", "white"];

    private static string? Hex(Color? color) => color is { } c && c != Color.Default
        ? PaletteNames.Contains(c.ToMarkup()) ? c.ToMarkup() : c.ToHex()
        : null;

    // SCREENSHOT_THEME=light dumps the light palette; the default is dark.
    private static IDisposable UseScreenshotTheme()
    {
        Theme.Use(Palette.For(Environment.GetEnvironmentVariable("SCREENSHOT_THEME"), null) ?? Palette.Dark);
        return new ResetTheme();
    }

    private sealed class ResetTheme : IDisposable
    {
        public void Dispose() => Theme.Use(Palette.Dark);
    }
}
