using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// p0131c-pre: collapses Review/Final phase assignments into Plan for presets
/// that don't run those phases (mad-discussion, legal-analysis). Without this,
/// the LLM-emitted TriageOutput's Review/Final assignments would be silently
/// dropped at <see cref="PhaseCommandExpander"/> time. Order of merge:
///   1. Plan-phase Lead stays Lead.
///   2. Plan-phase Analysts.
///   3. Review-phase analysts (now Plan analysts).
///   4. Review-phase reviewers (now Plan analysts).
///   5. Final-phase analysts (now Plan analysts).
///   6. Final-phase reviewers (now Plan analysts).
///   7. Filter from any phase declared one (Plan wins, then Review, then Final).
/// Duplicate skill names across positions are deduplicated last-wins.
/// </summary>
public sealed class SinglePhaseCollapser
{
    public TriageOutput Collapse(TriageOutput output)
    {
        var plan = output.Phases.TryGetValue(PipelinePhase.Plan, out var p) ? p : EmptyPhase();
        var review = output.Phases.TryGetValue(PipelinePhase.Review, out var r) ? r : EmptyPhase();
        var final = output.Phases.TryGetValue(PipelinePhase.Final, out var f) ? f : EmptyPhase();

        var lead = plan.Lead;
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (lead is not null) assigned.Add(lead);

        var analysts = new List<string>();
        AddUnique(analysts, plan.Analysts, assigned);
        AddUnique(analysts, review.Analysts, assigned);
        AddUnique(analysts, review.Reviewers, assigned);
        AddUnique(analysts, final.Analysts, assigned);
        AddUnique(analysts, final.Reviewers, assigned);

        var filter = plan.Filter ?? review.Filter ?? final.Filter;

        var collapsed = new PhaseAssignment(
            Lead: lead,
            Analysts: analysts,
            Reviewers: Array.Empty<string>(),
            Filter: filter);

        return new TriageOutput(
            Phases: new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = collapsed,
            },
            Confidence: output.Confidence,
            Rationale: output.Rationale);
    }

    private static void AddUnique(List<string> dest, IReadOnlyList<string> src, HashSet<string> seen)
    {
        foreach (var name in src)
        {
            if (seen.Add(name)) dest.Add(name);
        }
    }

    private static PhaseAssignment EmptyPhase() =>
        new(Lead: null, Analysts: Array.Empty<string>(),
            Reviewers: Array.Empty<string>(), Filter: null);
}
