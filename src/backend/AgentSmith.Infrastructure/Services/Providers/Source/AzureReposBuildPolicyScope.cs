using Microsoft.TeamFoundation.Policy.WebApi;
using Newtonsoft.Json.Linq;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-24-f9c5d: the build definitions that validate pull requests into a branch of a repository — the enabled,
/// undeleted build-validation policies whose scope names this repository (or none: every repository of the project) and
/// the branch exactly or by prefix. Filtered here: the SDK's read takes no repository or ref.
/// </summary>
public static class AzureReposBuildPolicyScope
{
    public sealed record Validation(int DefinitionId, string Name, bool Required);

    public static IReadOnlyList<Validation> Validating(IEnumerable<PolicyConfiguration> configurations, Guid repositoryId, string refName)
    {
        ArgumentNullException.ThrowIfNull(configurations);
        return [.. configurations
            .Where(c => c.IsEnabled && !c.IsDeleted && c.Type?.Id == Guid.Parse(AzureReposCiPolicies.BuildPolicyType))
            .Where(c => Scopes(c.Settings).Any(s => Covers(s, repositoryId, refName)))
            .Select(c => (Config: c, Id: c.Settings?["buildDefinitionId"]?.Type == JTokenType.Integer ? c.Settings["buildDefinitionId"]!.Value<int>() : 0))
            .Where(x => x.Id > 0)
            .Select(x => new Validation(x.Id, x.Config.Settings?["displayName"]?.Value<string>() is { Length: > 0 } name ? name : $"build {x.Id}",
                x.Config.IsBlocking))
            .DistinctBy(v => v.DefinitionId)];
    }

    private static IEnumerable<JObject> Scopes(JObject? settings) =>
        settings?["scope"] is JArray scopes ? scopes.OfType<JObject>() : [];

    private static bool Covers(JObject scope, Guid repositoryId, string refName)
    {
        var repo = scope["repositoryId"];
        if (repo is not null && repo.Type != JTokenType.Null
            && (!Guid.TryParse(repo.Value<string>(), out var id) || id != repositoryId)) return false;
        var name = scope["refName"]?.Value<string>();
        if (string.IsNullOrEmpty(name)) return true;
        return string.Equals(scope["matchKind"]?.Value<string>(), "prefix", StringComparison.OrdinalIgnoreCase)
            ? refName.StartsWith(name, StringComparison.OrdinalIgnoreCase)
            : string.Equals(refName, name, StringComparison.OrdinalIgnoreCase);
    }
}
