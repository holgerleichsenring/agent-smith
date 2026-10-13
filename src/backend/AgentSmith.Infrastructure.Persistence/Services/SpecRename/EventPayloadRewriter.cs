using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: a RunEvents row — its type name, and the payload of the event types that
/// carry a spec name. Scoped by event type: the LLM, tool and cost events keep their
/// execution stage because no row of theirs is ever opened here.
/// </summary>
public sealed class EventPayloadRewriter(
    SpecRenameJson json, CheckpointJsonRewriter checkpoints, SpecMemberRewriter members)
{
    private static readonly IReadOnlyDictionary<string, Type> Walked = new Dictionary<string, Type>
    {
        [nameof(EventType.SpecStateChanged)] = typeof(SpecStateChangedEvent),
        [nameof(EventType.SpecRecorded)] = typeof(SpecRecordedEvent),
        [nameof(EventType.SpecReviewed)] = typeof(SpecReviewedEvent),
        [nameof(EventType.StepStarted)] = typeof(StepStartedEvent),
        [nameof(EventType.DecisionLogged)] = typeof(DecisionLoggedEvent),
    };

    /// <summary>The declared type names whose payloads this rewrites.</summary>
    public IReadOnlyList<string> TypeNames { get; } =
    [
        .. Walked.Keys, nameof(EventType.RunCheckpointed), nameof(EventType.RunStoryRecorded),
    ];

    /// <summary>The rewritten payload of a row of <paramref name="typeName"/>, or null when nothing moved.</summary>
    public string? Rewrite(string typeName, string? payload) => typeName switch
    {
        nameof(EventType.RunCheckpointed) => json.Rewrite(payload, node => node is JsonObject e
            && (json.RewriteNested(e, nameof(RunCheckpointedEvent.RemainingCommandsJson), checkpoints.RewriteCommandsJson)
                | json.RewriteNested(e, nameof(RunCheckpointedEvent.ContextJson), checkpoints.RewriteContext))),
        nameof(EventType.RunStoryRecorded) => json.Rewrite(payload, node => node is JsonObject e
            && json.RewriteNested(e, nameof(RunStoryRecordedEvent.AcceptanceJson),
                acceptance => json.Rewrite(acceptance, typeof(AcceptanceView), JsonNamingPolicy.CamelCase))),
        _ when Walked.TryGetValue(typeName, out var type) =>
            json.Rewrite(payload, node => members.Rewrite(node, type, policy: null) | RenameCommand(node)),
        _ => null,
    };

    private static bool RenameCommand(JsonNode node)
    {
        if (node is not JsonObject e || e[nameof(StepStartedEvent.CommandName)] is not JsonValue value
            || !value.TryGetValue<string>(out var stored) || SpecRenameTable.CommandName(stored) is not { } current) return false;
        e[nameof(StepStartedEvent.CommandName)] = current;
        return true;
    }
}
