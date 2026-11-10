using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;

namespace ServiceBusConsole.CommandLine;

/// <summary>
/// Whether the namespace can be used from here, step by step until one fails: Azure sign-in and the namespace, DNS
/// (private or public address), the AMQP port, then a peek, which needs a data-plane role.
/// </summary>
public sealed record CheckCommand : NamespaceCommand
{
    private const int AmqpsPort = 5671;
    private const int EmulatorAmqpPort = 5672;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private sealed record Step(string Name, bool Ok, string Detail, string? Hint = null);

    public override async Task<int> RunAsync(CommandContext context)
    {
        var steps = new List<Step>();
        await foreach (var step in StepsAsync(context))
        {
            steps.Add(step);
            if (!context.Json) await WriteAsync(context.Out, step);
            if (!step.Ok) break;
        }
        var ok = steps.All(s => s.Ok);
        if (context.Json)
            await context.WriteJsonAsync(new JsonObject
            {
                ["ok"] = ok,
                ["checks"] = new JsonArray([.. steps.Select(s => (JsonNode)new JsonObject
                {
                    ["name"] = s.Name, ["ok"] = s.Ok, ["detail"] = s.Detail, ["hint"] = s.Hint,
                })]),
            });
        return ok ? ExitCodes.Ok : ExitCodes.Failed;
    }

    private static async IAsyncEnumerable<Step> StepsAsync(CommandContext context)
    {
        var ct = context.Token;
        AzureSubscription subscription;
        NamespaceInfo info;
        Step resolved;
        try
        {
            (subscription, info) = await context.Target.ResolveAsync(context.Services.Namespaces, ct);
            resolved = new(CommandLineStrings.CheckCommand_Namespace, true, $"{subscription.Name} › {info.Name} ({info.FullyQualifiedNamespace})");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            (subscription, info) = (AzureSubscription.Emulator, NamespaceInfo.Emulator);
            resolved = new(CommandLineStrings.CheckCommand_Namespace, false, ex.Message.Split('\n')[0], CommandLineStrings.CheckCommand_SignInHint);
        }
        yield return resolved;
        if (!resolved.Ok) yield break;

        var host = info.FullyQualifiedNamespace;
        IPAddress[] addresses;
        string? dnsError = null;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, ct);
        }
        catch (SocketException ex)
        {
            addresses = [];
            dnsError = ex.Message;
        }
        if (addresses is [])
        {
            yield return new(CommandLineStrings.CheckCommand_Dns, false, dnsError ?? CommandLineStrings.FormatCheckCommand_NoAddress(host), CommandLineStrings.CheckCommand_DnsHint);
            yield break;
        }
        var isPrivate = addresses.All(IsPrivate);
        var where = isPrivate ? CommandLineStrings.CheckCommand_Private : CommandLineStrings.CheckCommand_Public;
        yield return new(CommandLineStrings.CheckCommand_Dns, true, $"{host} → {string.Join(", ", addresses.Select(a => a.ToString()))} ({where})");

        // The emulator speaks plain AMQP; Azure only AMQP over TLS.
        var port = subscription.IsEmulator ? EmulatorAmqpPort : AmqpsPort;
        string? connectError = null;
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, ct).AsTask().WaitAsync(ConnectTimeout, ct);
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException)
        {
            connectError = ex.Message;
        }
        yield return connectError is null
            ? new(CommandLineStrings.CheckCommand_Port, true, $"{host}:{port}")
            : new(CommandLineStrings.CheckCommand_Port, false, $"{host}:{port}: {connectError}",
                isPrivate ? CommandLineStrings.CheckCommand_PortPrivateHint : CommandLineStrings.CheckCommand_PortPublicHint);
        if (connectError is not null) yield break;

        Step peek;
        try
        {
            var ns = await context.OpenAsync();
            var source = await SampleSourceAsync(ns, ct);
            if (source is null)
                peek = new(CommandLineStrings.CheckCommand_Peek, true, CommandLineStrings.CheckCommand_NothingToPeek);
            else
            {
                var count = await ns.Messages.PeekAsync(source, 1, ct: ct).Select(page => page.Count).SumAsync(ct);
                peek = new(CommandLineStrings.CheckCommand_Peek, true, CommandLineStrings.FormatCheckCommand_Peeked(count, source.DisplayName));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            peek = new(CommandLineStrings.CheckCommand_Peek, false, ErrorText.Of(ex).Split('\n')[0],
                ErrorText.IsFirewall(ex) ? null : CommandLineStrings.CheckCommand_PeekHint);
        }
        yield return peek;
    }

    // A dead-letter queue when there is one with messages, else any queue or subscription.
    private static async Task<MessageSource?> SampleSourceAsync(IServiceBusNamespace ns, CancellationToken ct)
    {
        var queues = await ns.Queues.GetQueuesAsync(ct).ToListAsync(ct);
        if (queues.FirstOrDefault(q => q.DeadLetterMessageCount > 0) is { } withDlq) return new MessageSource.Queue(withDlq.Name, IsDlq: true);
        if (queues.FirstOrDefault() is { } queue) return new MessageSource.Queue(queue.Name);
        if (ns is not IHasTopics { Topics: var topics }) return null;
        return await topics.GetAllSubscriptionsAsync(ct).FirstOrDefaultAsync(ct) is { } sub
            ? new MessageSource.Subscription(sub.TopicName, sub.Name)
            : null;
    }

    private static Task WriteAsync(TextWriter writer, Step step) =>
        writer.WriteLineAsync($"{(step.Ok ? "✓" : "✗")} {step.Name}: {step.Detail}" + (step.Hint is { } hint ? $"\n  → {hint}" : ""));

    public static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetworkV6) return address.IsIPv6UniqueLocal || address.IsIPv6LinkLocal;
        var b = address.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
