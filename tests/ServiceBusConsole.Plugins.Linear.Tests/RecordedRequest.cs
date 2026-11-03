using System.Text.Json.Nodes;

namespace ServiceBusConsole.Plugins.Linear.Tests;

internal sealed record RecordedRequest(
    HttpMethod Method, Uri? Uri, string? Authorization, string? ContentType, string Operation, string Query, JsonNode Variables);
