using System.Text.Json;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-25-9dbbf: one GraphQL query over the provider's own Octokit connection, answered as its
/// <c>data</c> element. A reply carrying an errors array throws, because GraphQL fails as HTTP 200.
/// </summary>
public sealed class GitHubGraphQlQuery(IGitHubClient client, Uri endpoint)
{
    public async Task<JsonElement> RunAsync(string query, object variables, string what)
    {
        var response = await client.Connection.Post<string>(
            endpoint, new { query, variables }, "application/json", "application/json");
        using var doc = JsonDocument.Parse(response.Body);
        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            throw new InvalidOperationException($"GitHub GraphQL refused the {what} query: {errors.GetRawText()}");
        return doc.RootElement.GetProperty("data").Clone();
    }
}
