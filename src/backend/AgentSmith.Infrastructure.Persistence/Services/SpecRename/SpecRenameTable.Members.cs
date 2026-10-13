using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ah: the members renamed per DECLARING type, and the enum members stored by name.
/// Type-scoped, never name-scoped: Phase is still the execution stage of an LLM event or a cost
/// record, so a member moves only where the type that owns it is listed here. Declaration casing;
/// a naming policy is applied where the stored text was written camelCase. Every static here is
/// declared before its first use in this file, so no cross-file initialisation order is relied on.
/// </summary>
public static partial class SpecRenameTable
{
    private static readonly NameRename SpecId = new("PhaseId", "SpecId");
    private static readonly NameRename Specs = new("Phases", "Specs");

    /// <summary>Old member name to current, per declaring type.</summary>
    public static IReadOnlyDictionary<Type, IReadOnlyList<NameRename>> Members { get; } =
        new Dictionary<Type, IReadOnlyList<NameRename>>
        {
            [typeof(SpecSet)] = [Specs, new("ExecutedPhaseIds", "ExecutedSpecIds")],
            [typeof(SpecSequenceProgress)] = [Specs],
            [typeof(SpecProgress)] = [SpecId],
            [typeof(DerivedSpec)] = [SpecId],
            [typeof(RunShortfall)] = [new("PhaseCount", "SpecCount")],
            [typeof(CarriedSegment)] = [SpecId],
            [typeof(SetDivergence)] = [Specs],
            [typeof(ProposalFinding)] = [SpecId],
            [typeof(SpecDraft)] = [SpecId],
            [typeof(StepStartedEvent)] = [SpecId],
            [typeof(SpecStateChangedEvent)] = [SpecId],
            [typeof(SpecRecordedEvent)] = [SpecId],
            [typeof(SpecReviewedEvent)] = [SpecId],
            [typeof(DecisionLoggedEvent)] = [SpecId],
            [typeof(PlannedStepView)] = [SpecId],
            [typeof(DeclinedCriterionView)] = [new("Phase", "Spec")],
            [typeof(DeclinedCriterion)] = [SpecId],
            [typeof(RunAccounts)] = [Specs],
            [typeof(SpecAccounts)] = [SpecId],
            [typeof(RunSpecReviews)] = [Specs],
            [typeof(SpecReviewEntry)] = [SpecId],
        };

    /// <summary>RunEvents.Type: the EventType names of codes 79-81 (the codes never moved).</summary>
    public static IReadOnlyList<NameRename> EventTypeNames { get; } =
    [
        new("PhaseStateChanged", "SpecStateChanged"),
        new("PhaseRecorded", "SpecRecorded"),
        new("PhaseReviewed", "SpecReviewed"),
    ];

    /// <summary>Old enum member name to current, per enum type, for an enum stored by name.</summary>
    public static IReadOnlyDictionary<Type, IReadOnlyList<NameRename>> EnumMembers { get; } =
        new Dictionary<Type, IReadOnlyList<NameRename>>
        {
            [typeof(SpecDialogTurnKind)] = [new("PhaseProposal", "SpecProposal")],
            [typeof(EventType)] = EventTypeNames,
        };
}
