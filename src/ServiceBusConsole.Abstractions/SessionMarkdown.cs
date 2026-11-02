using System.Globalization;
using System.Text;

namespace ServiceBusConsole.Abstractions;

/// <summary>A <see cref="SessionSnapshot"/> as Markdown: where the user is, the message, recent activity (newest last).</summary>
public static class SessionMarkdown
{
    public static string Format(SessionSnapshot snapshot)
    {
        var where = Where(snapshot).ToList();
        var sb = new StringBuilder()
            .Append("# Azure Service Bus session\n\n")
            .Append(CultureInfo.InvariantCulture, $"Captured {ContextText.Time(snapshot.At)}.\n\n")
            .Append("## Where\n\n")
            .Append(where.Count == 0 ? "Not connected.\n" : string.Concat(where.Select(line => $"- {line}\n")));

        if (snapshot.Message is { } message) sb.Append('\n').Append(MessageMarkdown.Format(message));

        sb.Append("\n## Recent activity\n\n");
        return (snapshot.Activity.Count == 0
            ? sb.Append("None.\n")
            : sb.AppendJoin("", snapshot.Activity.Select(Line))).ToString();
    }

    private static string Line(ActivityEntry entry) =>
        $"- {ContextText.Time(entry.At)} {entry.Kind}{(entry.Source is { } s ? $" ({s.DisplayName})" : "")}: {entry.Text.ReplaceLineEndings(" ")}\n";

    private static IEnumerable<string> Where(SessionSnapshot snapshot)
    {
        if (snapshot.Subscription is { } subscription)
            yield return subscription.Id is { } id ? $"Azure subscription: {subscription.Name} (`{id}`)" : $"Azure subscription: {subscription.Name}";
        if (snapshot.Namespace is { } ns)
        {
            string[] details = [ns.Sku ?? "", ns.Location, ns.ResourceGroup.Length > 0 ? $"resource group {ns.ResourceGroup}" : ""];
            var extra = string.Join(", ", details.Where(d => d.Length > 0));
            yield return $"Namespace: {ns.Name} (`{ns.FullyQualifiedNamespace}`)" + (extra.Length > 0 ? $", {extra}" : "");
        }
        if (snapshot.Source is { } source)
            yield return source.Match(
                q => $"Queue: `{q.Name}`",
                s => $"Topic subscription: `{s.Name}` of topic `{s.Topic}`") + (source.IsDlq ? ", dead-letter queue" : "");
        if (snapshot.Mode is { } mode) yield return $"Mode: {mode}";
    }
}
