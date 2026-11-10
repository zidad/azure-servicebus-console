using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ServiceBusConsole.CommandLine;

/// <summary>One run of a namespace command: its options, the namespace once opened, and where output goes.</summary>
public sealed class CommandContext(NamespaceInput input, CommandServices services, CancellationToken ct) : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private IServiceBusNamespace? _opened;

    public CommandServices Services { get; } = services;
    public NamespaceTarget Target => input.Target;
    public bool Json => input.Json;
    public CancellationToken Token { get; } = ct;
    public TextWriter Out => Services.Out;
    public TextWriter Error => Services.Error;

    /// <summary>Resolves the target and opens it; once per run.</summary>
    public async Task<IServiceBusNamespace> OpenAsync()
    {
        if (_opened is { } opened) return opened;
        var (_, info) = await Target.ResolveAsync(Services.Namespaces, Token);
        _opened = Services.Opener.Open(info);
        return _opened;
    }

    public Redaction Redaction(NamespaceInfo ns) => CommandLine.Redaction.For(ns, Services.Configuration);

    /// <summary>A protected namespace (<c>cli.protect</c>) changes only with <c>--confirm</c> naming it.</summary>
    public void RequireConfirmation(NamespaceInfo ns)
    {
        var isProtected = Glob.List(Services.Configuration[ConfigKeys.Protect.ConfigurationPath])
            .Any(g => g.Matches(ns.Name) || g.Matches(ns.FullyQualifiedNamespace));
        if (isProtected && !string.Equals(input.Confirm, ns.Name, StringComparison.OrdinalIgnoreCase))
            throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.FormatCommandContext_Protected(ns.Name, ConfigKeys.Protect.Key));
    }

    public Task WriteJsonAsync(JsonNode node) => Out.WriteLineAsync(node.ToJsonString(JsonOptions));

    public static string Format(JsonNode? node) => node?.ToJsonString(JsonOptions) ?? "null";

    public ValueTask DisposeAsync() => _opened?.DisposeAsync() ?? ValueTask.CompletedTask;
}
