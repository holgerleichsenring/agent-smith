using System.Text.Json;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-7a92a: the status checks a branch requires — its rulesets' required_status_checks rules (Metadata read) and its
/// classic branch protection's required contexts (Administration read). Either one the token cannot read, or that the branch
/// does not have, adds nothing; a required check is named by its context, as statusCheckRollup names a check run or status.
/// </summary>
public sealed class GitHubRequiredContexts(IGitHubClient client, GitHubRepoAddress address, ILogger logger)
{
    public async Task<IReadOnlySet<string>> ReadAsync(string branch, CancellationToken ct)
    {
        var repo = $"repos/{address.Owner}/{address.Repo}";
        var escaped = Uri.EscapeDataString(branch);
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (await GetAsync($"{repo}/rules/branches/{escaped}", ct) is { ValueKind: JsonValueKind.Array } rules)
            foreach (var rule in rules.EnumerateArray().Where(r => Text(r, "type") == "required_status_checks"))
                names.UnionWith(Contexts(rule, "parameters", "required_status_checks"));
        if (await GetAsync($"{repo}/branches/{escaped}/protection/required_status_checks", ct) is { ValueKind: JsonValueKind.Object } classic)
        {
            if (classic.TryGetProperty("contexts", out var contexts) && contexts.ValueKind == JsonValueKind.Array)
                names.UnionWith(contexts.EnumerateArray().Select(c => c.GetString()).OfType<string>());
            names.UnionWith(Contexts(classic, null, "checks"));
        }
        return names;
    }

    private async Task<JsonElement?> GetAsync(string path, CancellationToken ct)
    {
        try
        {
            var response = await client.Connection.Get<string>(new Uri(path, UriKind.Relative), null, "application/vnd.github+json", ct);
            using var doc = JsonDocument.Parse(response.HttpResponse.Body as string ?? "null");
            return doc.RootElement.Clone();
        }
        catch (ApiException ex)
        {
            // Not protected (404) or not readable with this token (403): this source requires nothing the read can see.
            logger.LogDebug(ex, "{Repo}: {Path} not read ({Status}); it adds no required check", address.FullName, path, ex.StatusCode);
            return null;
        }
    }

    private static IEnumerable<string> Contexts(JsonElement e, string? parent, string list)
    {
        if (parent is not null && (!e.TryGetProperty(parent, out e) || e.ValueKind != JsonValueKind.Object)) return [];
        return e.TryGetProperty(list, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Select(i => Text(i, "context")).OfType<string>() : [];
    }

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
