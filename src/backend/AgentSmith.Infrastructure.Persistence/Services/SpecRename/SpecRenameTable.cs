namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ah: every stored name the spec rename moved, frozen as it stood when the readers
/// of the old names were removed. Nothing but the store rewrite reads it: no reader of owned data
/// accepts an old name any more, so a store last written before 2026-10-12-967ab converts here,
/// once, and is then read in the spec names alone. The members renamed per declaring type are in
/// the other half of this class.
/// </summary>
public static partial class SpecRenameTable
{
    /// <summary>The kind an outcome proposal and a filing were written under before the rename.</summary>
    public const string OldSpecKind = "phase";

    /// <summary>The kind they are written under now.</summary>
    public const string SpecKind = "spec";

    /// <summary>The ContextKeys values a checkpoint entry was stored under.</summary>
    public static IReadOnlyList<NameRename> ContextKeys { get; } =
    [
        new("PhaseSpec", "CurrentSpec"),
        new("PhaseStartHeads", "SpecStartHeads"),
        new("PhaseReviewFindings", "SpecReviewFindings"),
        new("PhaseReviewFixPass", "SpecReviewFixPass"),
        new("PhaseReviewReverted", "SpecReviewReverted"),
        new("PhaseAccountsSnapshot", "SpecAccountsSnapshot"),
        new("RunPhaseReviews", "RunSpecReviews"),
        new("PhaseCommands", "SpecRunCommands"),
        new("PhaseAccounts", "SpecAccounts"),
        new("PhaseRepairAttempted", "SpecRepairAttempted"),
        new("PhaseAppliesTo", "SpecAppliesTo"),
        new("ActivePhaseStep", "ActiveStep"),
    ];

    /// <summary>The full CLR type names a checkpoint entry's assembly-qualified type was written with.</summary>
    public static IReadOnlyList<NameRename> TypeNames { get; } =
    [
        new("AgentSmith.Contracts.Models.PhaseDraft", "AgentSmith.Contracts.Models.SpecDraft"),
        new("AgentSmith.Contracts.Models.PhaseStep", "AgentSmith.Contracts.Models.SpecStep"),
        new("AgentSmith.Contracts.Models.PhaseFact", "AgentSmith.Contracts.Models.SpecFact"),
        new("AgentSmith.Contracts.Specs.SpecPhase", "AgentSmith.Contracts.Specs.DerivedSpec"),
        new("AgentSmith.Contracts.Specs.PhaseProgress", "AgentSmith.Contracts.Specs.SpecProgress"),
        new("AgentSmith.Contracts.Specs.PhaseRunState", "AgentSmith.Contracts.Specs.SpecRunState"),
        new("AgentSmith.Application.Services.Specs.PhaseCommandLog", "AgentSmith.Application.Services.Specs.SpecRunCommandLog"),
        new("AgentSmith.Application.Services.Specs.PhaseCommandEntry", "AgentSmith.Application.Services.Specs.SpecRunCommandEntry"),
        new("AgentSmith.Domain.Models.RunPhaseReviews", "AgentSmith.Domain.Models.RunSpecReviews"),
        new("AgentSmith.Domain.Models.PhaseReviewEntry", "AgentSmith.Domain.Models.SpecReviewEntry"),
        new("AgentSmith.Domain.Models.PhaseReviewReport", "AgentSmith.Domain.Models.SpecReviewReport"),
        new("AgentSmith.Domain.Models.PhaseFinding", "AgentSmith.Domain.Models.SpecFinding"),
    ];

    /// <summary>The spec-block command names a step row, a planned step or a checkpoint stored.</summary>
    public static IReadOnlyList<NameRename> CommandNames { get; } =
    [
        new("WritePhaseRecordCommand", "WriteSpecRecordCommand"),
        new("VerifyPhaseCommand", "VerifySpecCommand"),
        new("CommitPhaseWorkCommand", "CommitSpecWorkCommand"),
        new("CheckPhasePremisesCommand", "CheckSpecPremisesCommand"),
        new("ReviewPhaseDiffCommand", "ReviewSpecDiffCommand"),
        new("PhaseSequenceCommand", "SpecSequenceCommand"),
        new("SelectPhaseCommand", "SelectSpecCommand"),
        new("PhaseSpecGateCommand", "SpecGateCommand"),
    ];

    /// <summary>RunArtifacts.Kind prefixes of a spec's record and review.</summary>
    public static IReadOnlyList<NameRename> ArtifactPrefixes { get; } =
    [
        new("phase_record:", "spec_record:"),
        new("phase_review:", "spec_review:"),
    ];

    /// <summary>
    /// The current name of a stored command name — a ":param" suffix kept — or null when it
    /// carries no old name.
    /// </summary>
    public static string? CommandName(string stored)
    {
        var colon = stored.IndexOf(':');
        var baseName = colon < 0 ? stored : stored[..colon];
        return CommandNames.FirstOrDefault(r => r.Old == baseName) is { } rename
            ? rename.New + (colon < 0 ? string.Empty : stored[colon..])
            : null;
    }
}
