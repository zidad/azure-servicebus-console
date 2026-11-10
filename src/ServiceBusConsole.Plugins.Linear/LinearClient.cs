using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ServiceBusConsole.Plugins.Linear.Resources;

namespace ServiceBusConsole.Plugins.Linear;

/// <summary>The few calls of Linear's GraphQL API the plugin makes; the API key goes with each call.</summary>
internal sealed class LinearClient(HttpClient http)
{
    public static readonly Uri Endpoint = new("https://api.linear.app/graphql");

    public static string RejectedKey =>
        Strings.FormatLinearClient_RejectedKey(LinearPlugin.PluginId, LinearSettings.ApiKey, LinearPlugin.ApiKeyVariable);

    private const string TeamQuery =
        "query Team($key: String!) { teams(filter: { key: { eqIgnoreCase: $key } }) { nodes { id key } } }";

    private const string LabelsQuery =
        "query Labels($names: [String!]!) { issueLabels(filter: { name: { in: $names } }) { nodes { id name team { id } } } }";

    private const string CreateMutation =
        "mutation Create($input: IssueCreateInput!) { issueCreate(input: $input) { success issue { identifier url } } }";

    public async Task<string> TeamIdAsync(string apiKey, string key, CancellationToken ct)
    {
        var data = await QueryAsync(apiKey, TeamQuery, new JsonObject { ["key"] = key }, ct);
        return Nodes(data["teams"]).Select(n => n?["id"]?.GetValue<string>()).OfType<string>().FirstOrDefault()
            ?? throw new LinearException(Strings.FormatLinearClient_NoTeam(key));
    }

    /// <summary>The ids of the labels named, the team's label over a workspace one of the same name.</summary>
    public async Task<IReadOnlyList<string>> LabelIdsAsync(string apiKey, string teamId, IReadOnlyList<string> names, CancellationToken ct)
    {
        if (names.Count == 0) return [];
        var data = await QueryAsync(apiKey, LabelsQuery, new JsonObject { ["names"] = new JsonArray([.. names.Select(n => JsonValue.Create(n))]) }, ct);
        var labels = Nodes(data["issueLabels"])
            .Select(n => (Id: n?["id"]?.GetValue<string>(), Name: n?["name"]?.GetValue<string>(), Team: n?["team"]?["id"]?.GetValue<string>()))
            .Where(l => l.Team is null || l.Team == teamId)
            .ToList();
        var found = names.Select(name => (Name: name, Id: labels
                .Where(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(l => l.Team is null)
                .Select(l => l.Id)
                .FirstOrDefault()))
            .ToList();
        return found.Where(f => f.Id is null).Select(f => f.Name).ToList() is { Count: > 0 } missing
            ? throw new LinearException(Strings.FormatLinearClient_NoLabels(string.Join(", ", missing)))
            : [.. found.Select(f => f.Id).OfType<string>().Distinct()];
    }

    public async Task<CreatedIssue> CreateIssueAsync(string apiKey, NewIssue issue, CancellationToken ct)
    {
        var input = new JsonObject
        {
            ["teamId"] = issue.TeamId,
            ["title"] = issue.Title,
            ["description"] = issue.Description,
        };
        if (issue.LabelIds.Count > 0) input["labelIds"] = new JsonArray([.. issue.LabelIds.Select(id => JsonValue.Create(id))]);

        var created = (await QueryAsync(apiKey, CreateMutation, new JsonObject { ["input"] = input }, ct))["issueCreate"];
        return created?["issue"] is { } node
               && node["identifier"]?.GetValue<string>() is { } identifier
               && Uri.TryCreate(node["url"]?.GetValue<string>(), UriKind.Absolute, out var url)
            ? new CreatedIssue(identifier, url)
            : throw new LinearException(Strings.LinearClient_NotCreated);
    }

    private async Task<JsonObject> QueryAsync(string apiKey, string query, JsonObject variables, CancellationToken ct)
    {
        var body = new JsonObject { ["query"] = query, ["variables"] = variables };
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        // Personal API keys go as is; only OAuth tokens take "Bearer".
        request.Headers.TryAddWithoutValidation("Authorization", apiKey);

        using var response = await http.SendAsync(request, ct);
        var json = Parse(await response.Content.ReadAsStringAsync(ct));
        var errors = Nodes(json?["errors"]).ToList();

        if (response.StatusCode == HttpStatusCode.Unauthorized || errors.Any(IsAuthenticationError))
            throw new LinearException(RejectedKey);
        if (errors.Select(e => e?["message"]?.GetValue<string>()).OfType<string>().ToList() is { Count: > 0 } messages)
            throw new LinearException(Strings.FormatLinearClient_Errors(string.Join("; ", messages)));
        if (!response.IsSuccessStatusCode)
            throw new LinearException(Strings.FormatLinearClient_HttpError((int)response.StatusCode, response.ReasonPhrase));
        return json?["data"] as JsonObject ?? throw new LinearException(Strings.LinearClient_NoData);
    }

    private static bool IsAuthenticationError(JsonNode? error) =>
        error?["extensions"]?["code"]?.GetValue<string>() is "AUTHENTICATION_ERROR";

    private static IEnumerable<JsonNode?> Nodes(JsonNode? node) =>
        (node is JsonArray array ? array : node?["nodes"] as JsonArray) ?? [];

    private static JsonNode? Parse(string text)
    {
        try { return JsonNode.Parse(text); }
        catch (JsonException) { return null; }
    }
}
