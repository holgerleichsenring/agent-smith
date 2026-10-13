using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: the Runs row — its planned steps (PlannedStepView, camelCase, whose command
/// names map through <see cref="SpecRenameTable.CommandName"/>), the request a capacity-queued run waits
/// with, and the acceptance the run story projected (AcceptanceView, camelCase).
/// </summary>
public sealed class RunSpecNames(
    SpecRenameJson json, RequestContextJsonRewriter requests, SpecMemberRewriter members, RowPages pages)
    : IStoredSpecNameSurface
{
    private static readonly JsonNamingPolicy Camel = JsonNamingPolicy.CamelCase;

    public string Name => "Runs";

    public Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct) =>
        pages.RewriteAsync(db,
            db.Runs.Where(r => (r.PlannedStepsJson != null && r.PlannedStepsJson.Contains("hase"))
                || (r.QueuedRequestJson != null && r.QueuedRequestJson.Contains("hase"))
                || (r.AcceptanceJson != null && r.AcceptanceJson.Contains("hase"))),
            r => r.Id, Rewrite, ct);

    private bool Rewrite(Run row)
    {
        var planned = json.Rewrite(row.PlannedStepsJson, RewritePlanned);
        var request = requests.RewriteRequest(row.QueuedRequestJson);
        var acceptance = json.Rewrite(row.AcceptanceJson, typeof(AcceptanceView), Camel);
        row.PlannedStepsJson = planned ?? row.PlannedStepsJson;
        row.QueuedRequestJson = request ?? row.QueuedRequestJson;
        row.AcceptanceJson = acceptance ?? row.AcceptanceJson;
        return planned is not null || request is not null || acceptance is not null;
    }

    private bool RewritePlanned(JsonNode node)
    {
        var changed = members.Rewrite(node, typeof(List<PlannedStepView>), Camel);
        foreach (var step in (node as JsonArray ?? []).OfType<JsonObject>())
        {
            if (step["commandName"] is not JsonValue value || !value.TryGetValue<string>(out var stored)) continue;
            if (SpecRenameTable.CommandName(stored) is not { } current) continue;
            step["commandName"] = current;
            changed = true;
        }
        return changed;
    }
}
