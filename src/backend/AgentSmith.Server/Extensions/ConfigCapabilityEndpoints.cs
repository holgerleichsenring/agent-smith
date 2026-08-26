using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Server.Security;
using Microsoft.AspNetCore.Mvc;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0345c: the config studio's READ surface for everything a form needs before it can
/// be filled in — the backend-truth capabilities descriptor, the two draft validators,
/// and the repo picker's discovery cache. Nothing here writes, so none of it touches
/// the write guard.
/// </summary>
internal static class ConfigCapabilityEndpoints
{
    internal static WebApplication MapConfigCapabilityEndpoints(this WebApplication app)
    {
        // p0345c: the backend-truth capabilities descriptor the studio's forms
        // render from. Type/strategy/pipeline lists come from the enums + code-
        // defined presets; agent providers from the REGISTERED chat-client
        // builders — the same index ChatClientFactory resolves against.
        app.MapGet("/api/config/capabilities", ([FromServices] IEnumerable<IChatClientBuilder> builders) =>
            // 2026-08-25-1806: the closed permission catalog and the role names it is
            // already bundled under ride along, so the role-mapping form picks from them.
            Results.Ok(ConfigStudioCapabilities.Build(builders.SelectMany(b => b.SupportedTypes))
                with { Permissions = Permissions.All, BuiltInRoles = [.. BuiltInRoles.All.Keys] }))
           .Needs(Permissions.ConfigRead);

        // p0392: what the server would say about a draft the operator has not saved.
        // p0391a made the server report what is missing once it is running; the editor
        // that PRODUCED the configuration is a better place to hear it. Same rules, one
        // source — ConfigDraftRules calls the server's own rule objects, so the studio
        // never restates a requirement in TypeScript.
        app.MapPost("/api/config/projects/validate",
            ([FromBody] ProjectEntity draft, IConfigStore store, [FromServices] ConfigDraftRules rules) =>
                Results.Ok(Views(rules.ForProject(draft, store.Catalog))))
           .Needs(Permissions.ConfigWrite);

        app.MapPost("/api/config/trackers/validate",
            ([FromBody] TrackerEntity draft, [FromServices] ConfigDraftRules rules) =>
                Results.Ok(Views(rules.ForTracker(draft))))
           .Needs(Permissions.ConfigWrite);

        // p0345c: the repo picker's discovery cache — the p0281a last-good snapshot.
        // Unknown connection → 404; known-but-undiscovered → 200 with
        // discoveredAt null + empty repos (honest "not discovered yet").
        app.MapGet("/api/config/connections/{id}/repos",
            async (string id, IConfigStore store,
                [FromServices] IConnectionRepoSnapshotStore snapshots, CancellationToken ct) =>
            {
                if (store.GetConnections().All(c => c.Id != id))
                    return Results.NotFound(new { error = $"Unknown connection '{id}'." });
                var discovery = await snapshots.TryGetDiscoveryAsync(id, ct);
                return Results.Ok(new ConnectionReposView(
                    discovery?.DiscoveredAt,
                    discovery?.Repos.Select(r => new ConnectionRepoView(r.Name, r.DefaultBranch)).ToList()
                        ?? []));
            }).Needs(Permissions.ConfigRead);

        return app;
    }

    private static IReadOnlyList<Models.StartupFindingView> Views(
        IReadOnlyList<AgentSmith.Contracts.Models.Configuration.StartupFinding> findings) =>
        findings.Select(Models.StartupFindingView.From).ToList();
}
