using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace ServiceBusConsole.Plugins.Linear.Tests;

/// <summary>Answers Linear GraphQL calls by operation name and records each request; never reaches the network.</summary>
internal sealed class FakeLinearHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new()
    {
        ["Team"] = (HttpStatusCode.OK, """{"data":{"teams":{"nodes":[{"id":"team-1","key":"ENG"}]}}}"""),
        ["Labels"] = (HttpStatusCode.OK, """{"data":{"issueLabels":{"nodes":[]}}}"""),
        ["Create"] = (HttpStatusCode.OK, """{"data":{"issueCreate":{"success":true,"issue":{"identifier":"ENG-42","url":"https://linear.app/acme/issue/ENG-42/orders"}}}}"""),
    };

    public List<RecordedRequest> Requests { get; } = [];

    public FakeLinearHandler Respond(string operation, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses[operation] = (status, body);
        return this;
    }

    public RecordedRequest Request(string operation) => Requests.Single(r => r.Operation == operation);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken) : "";
        var json = JsonNode.Parse(body) ?? throw new InvalidOperationException("Empty request body");
        var query = json["query"]?.GetValue<string>() ?? "";
        var operation = query.Split(' ', '(')[1];
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.TryGetValues("Authorization", out var auth) ? string.Join(",", auth) : null,
            request.Content?.Headers.ContentType?.MediaType,
            operation,
            query,
            json["variables"] ?? new JsonObject()));
        var (status, responseBody) = _responses[operation];
        return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
    }
}
