using System.Globalization;
using System.Text.Json.Nodes;

namespace ServiceBusConsole.CommandLine;

/// <summary>
/// Queues, topics or subscriptions with their counts, live. Subscriptions add when they were last used and where they
/// forward to, and whether that queue or topic exists: a subscription forwarding nowhere fills its dead-letter queue.
/// </summary>
public sealed record ListCommand(ListKind Kind = ListKind.Entities, bool DeadLettersOnly = false) : NamespaceCommand
{
    public override async Task<int> RunAsync(CommandContext context)
    {
        var ct = context.Token;
        var ns = await context.OpenAsync();
        var topicService = ns is IHasTopics { Topics: var t } ? t : null;
        var wantQueues = Kind is ListKind.Entities or ListKind.Queues;
        var wantSubscriptions = Kind is ListKind.Entities or ListKind.Subscriptions && topicService is not null;

        if (Kind == ListKind.Topics)
        {
            if (topicService is null) throw new CommandFailure(ExitCodes.NotFound, CommandLineStrings.ListCommand_NoTopics);
            var topicList = await topicService.GetTopicsAsync(ct).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToListAsync(ct);
            await WriteTopicsAsync(context, topicList);
            return ExitCodes.Ok;
        }

        // Forward targets are checked against every queue and topic, also when only subscriptions are listed.
        var queues = await ns.Queues.GetQueuesAsync(ct).OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase).ToListAsync(ct);
        var rows = new List<Row>();
        if (wantQueues)
            rows.AddRange(queues.Where(q => !DeadLettersOnly || q.DeadLetterMessageCount > 0).Select(q => new Row(
                "queue", q.Name, q.Name, null, q.ActiveMessageCount, q.DeadLetterMessageCount, q.ScheduledMessageCount, q.TransferMessageCount, null, null, null, null)));
        if (wantSubscriptions && topicService is not null)
        {
            var subscriptions = await topicService.GetAllSubscriptionsAsync(ct).ToListAsync(ct);
            var shown = subscriptions.Where(s => !DeadLettersOnly || s.DeadLetterMessageCount > 0).ToList();
            var forwarding = shown.Count == 0 ? [] : await topicService.GetForwardingAsync(ct).ToListAsync(ct);
            var topicNames = shown.Count == 0 ? [] : await topicService.GetTopicsAsync(ct).Select(x => x.Name).ToListAsync(ct);
            var targets = queues.Select(q => q.Name).Concat(topicNames).ToHashSet(StringComparer.OrdinalIgnoreCase);
            rows.AddRange(shown.OrderBy(s => s.TopicName, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(s =>
            {
                var forward = forwarding.FirstOrDefault(f => f.TopicName == s.TopicName && f.Name == s.Name);
                var forwardTo = EntityName(forward?.ForwardTo);
                return new Row("subscription", $"{s.TopicName}/{s.Name}", s.Name, s.TopicName, s.ActiveMessageCount, s.DeadLetterMessageCount, null,
                    s.TransferMessageCount, s.AccessedAt, forwardTo, EntityName(forward?.ForwardDeadLetteredMessagesTo),
                    forwardTo is null ? null : targets.Contains(forwardTo));
            }));
        }

        if (context.Json) await context.WriteJsonAsync(new JsonArray([.. rows.Select(r => (JsonNode)r.ToJson())]));
        else await WriteTableAsync(context.Out, rows);
        return ExitCodes.Ok;
    }

    private sealed record Row(
        string Type, string Path, string Name, string? Topic, long Active, long DeadLetter, long? Scheduled, long Transfer,
        DateTimeOffset? AccessedAt, string? ForwardTo, string? ForwardDeadLetteredTo, bool? ForwardTargetExists)
    {
        public JsonObject ToJson()
        {
            var json = new JsonObject { ["type"] = Type, ["path"] = Path, ["name"] = Name };
            if (Topic is not null) json["topic"] = Topic;
            json["active"] = Active;
            json["deadLetter"] = DeadLetter;
            if (Scheduled is { } scheduled) json["scheduled"] = scheduled;
            json["transfer"] = Transfer;
            if (Type == "subscription")
            {
                json["accessedAt"] = AccessedAt is { } at ? Time(at) : null;
                json["forwardTo"] = ForwardTo;
                json["forwardDeadLetteredTo"] = ForwardDeadLetteredTo;
                json["forwardTargetExists"] = ForwardTargetExists;
            }
            return json;
        }
    }

    private static async Task WriteTableAsync(TextWriter writer, IReadOnlyList<Row> rows)
    {
        if (rows.Count == 0)
        {
            await writer.WriteLineAsync(CommandLineStrings.ListCommand_Empty);
            return;
        }
        var anySubscription = rows.Any(r => r.Type == "subscription");
        IReadOnlyList<string> headers = anySubscription
            ? [CommandLineStrings.ListCommand_Path, CommandLineStrings.ListCommand_Active, CommandLineStrings.ListCommand_DeadLetter, CommandLineStrings.ListCommand_Scheduled, CommandLineStrings.ListCommand_Accessed, CommandLineStrings.ListCommand_ForwardTo]
            : [CommandLineStrings.ListCommand_Path, CommandLineStrings.ListCommand_Active, CommandLineStrings.ListCommand_DeadLetter, CommandLineStrings.ListCommand_Scheduled];
        var table = rows.Select(r => (IReadOnlyList<string>)[.. new[]
        {
            r.Path, N(r.Active), N(r.DeadLetter), r.Scheduled is { } s ? N(s) : "",
        }.Concat(anySubscription
            ? [r.AccessedAt is { } at ? at.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "", Forward(r)]
            : [])]).ToList();
        await TextTable.WriteAsync(writer, headers, table, new HashSet<int> { 1, 2, 3 });
    }

    private static string Forward(Row row) => row switch
    {
        { ForwardTo: null } => "",
        { ForwardTargetExists: false } => CommandLineStrings.FormatListCommand_ForwardMissing(row.ForwardTo),
        _ => row.ForwardTo,
    };

    private static async Task WriteTopicsAsync(CommandContext context, IReadOnlyList<TopicInfo> topics)
    {
        if (context.Json)
        {
            await context.WriteJsonAsync(new JsonArray([.. topics.Select(t => (JsonNode)new JsonObject
            {
                ["type"] = "topic", ["path"] = t.Name, ["name"] = t.Name, ["subscriptions"] = t.SubscriptionCount, ["scheduled"] = t.ScheduledMessageCount,
            })]));
            return;
        }
        if (topics.Count == 0)
        {
            await context.Out.WriteLineAsync(CommandLineStrings.ListCommand_Empty);
            return;
        }
        await TextTable.WriteAsync(context.Out, [CommandLineStrings.ListCommand_Path, CommandLineStrings.ListCommand_Subscriptions, CommandLineStrings.ListCommand_Scheduled],
            [.. topics.Select(t => (IReadOnlyList<string>)[t.Name, N(t.SubscriptionCount), N(t.ScheduledMessageCount)])], new HashSet<int> { 1, 2 });
    }

    // The management API may give a forward target as a URL (sb://namespace/queue).
    private static string? EntityName(string? target) =>
        target is null ? null
        : Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.AbsolutePath.Trim('/') is { Length: > 0 } path ? path
        : target;

    private static string N(long n) => n.ToString(CultureInfo.InvariantCulture);

    private static string Time(DateTimeOffset time) => time.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
}
