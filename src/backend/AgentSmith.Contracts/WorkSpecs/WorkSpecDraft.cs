using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: what one derivation call produced — the artifact pair, plus the
/// ticket-embedded instructions that got NO SLOT. The transform is the point
/// where untrusted prose becomes a typed artifact: an instruction that is not a
/// requirement has nowhere to land, and is reported through the p0316 refusal
/// record instead of quietly becoming scope.
/// </summary>
public sealed record WorkSpecDraft(
    WorkSpecArtifact Artifact,
    IReadOnlyList<IgnoredInstruction> IgnoredInstructions);
