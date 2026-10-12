using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: the JSON columns of a SpecDialogSession. Their roots are Server types this
/// assembly cannot reference, so the root is spelled here and pinned to the Server's reader by
/// tests: the outcome (OutcomeProposalJson's camelCase Dto — kind, its accepted "phase" member, the
/// drafts and findings below it, walked as their Contracts types), the filing kind
/// (SpecDialogFiling, web defaults) and the transcript turn kind (SpecDialogTurnKind).
/// </summary>
public sealed class SpecDialogJsonRewriter(SpecRenameJson json)
{
    /// <summary>The kind a filed or proposed spec was written under before the rename.</summary>
    public const string OldSpecKind = "phase";

    /// <summary>The kind it is written under now.</summary>
    public const string SpecKind = "spec";

    private static readonly JsonNamingPolicy Camel = JsonNamingPolicy.CamelCase;

    /// <summary>LatestProposalJson / ConfirmedOutcomeJson; null when nothing moved.</summary>
    public string? RewriteOutcome(string? outcome) =>
        json.Rewrite(outcome, node => node is JsonObject dto
            && (RenameKind(dto) | json.Move(dto, OldSpecKind, SpecKind)
                | Walk(dto, SpecKind, typeof(SpecDraft)) | Walk(dto, "parent", typeof(SpecDraft))
                | Walk(dto, "children", typeof(List<SpecDraft>)) | Walk(dto, "findings", typeof(List<ProposalFinding>))));

    /// <summary>LatestFilingJson; null when nothing moved.</summary>
    public string? RewriteFiling(string? filing) =>
        json.Rewrite(filing, node => node is JsonObject record && RenameKind(record));

    /// <summary>TranscriptJson; null when nothing moved.</summary>
    public string? RewriteTranscript(string? transcript) =>
        json.Rewrite(transcript, node => node is JsonArray turns
            && turns.OfType<JsonObject>().Aggregate(false, (changed, turn) => Walk(turn, "kind", typeof(SpecDialogTurnKind)) | changed));

    private static bool RenameKind(JsonObject owner)
    {
        if (owner["kind"] is not JsonValue kind || !kind.TryGetValue<string>(out var stored) || stored != OldSpecKind) return false;
        owner["kind"] = SpecKind;
        return true;
    }

    private static bool Walk(JsonObject owner, string member, Type type) =>
        owner[member] is { } child && AcceptedNameRewriter.Rewrite(child, type, Camel);
}
