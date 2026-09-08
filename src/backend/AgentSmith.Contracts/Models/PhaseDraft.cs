namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0315e: one drafted phase spec inside an outcome proposal. Yaml is the
/// full schema-valid spec text (what p0315c files into the ticket body);
/// PhaseId / Goal / Requires are extracted for display and requires-edge
/// consistency checks.
/// </summary>
public sealed record PhaseDraft(
    string PhaseId,
    string Goal,
    string Yaml,
    IReadOnlyList<string> Requires)
{
    /// <summary>
    /// p0393a: the phase's done-criteria, read out of the same yaml. They are the
    /// run's ACCEPTANCE CONTRACT once the code pipeline derives a spec for every
    /// ticket — the list the keystone scores, the pull request renders and result.md
    /// reports against, in the slot p0328's negotiated expectation used to fill.
    /// </summary>
    public IReadOnlyList<string> Done { get; init; } = [];

    /// <summary>
    /// p0394a: the spec's ordered steps, read out of the same yaml. The ratified spec
    /// is the run's single planning artifact — these steps seed the progress ledger
    /// and render as the master's plan section, so they are parsed once here instead
    /// of re-parsed by every consumer of the draft.
    /// </summary>
    public IReadOnlyList<PhaseStep> Steps { get; init; } = [];

    /// <summary>
    /// 2026-09-07-b7e2: what the derivation LOOKED UP before writing, each line with the
    /// evidence it cites. Read from the spec's own <c>facts</c> key, admitted by the
    /// schema's open top level; a spec written before this phase has none.
    /// </summary>
    public IReadOnlyList<PhaseFact> Facts { get; init; } = [];

    /// <summary>
    /// 2026-09-07-b7e2: what the derivation stated WITHOUT a look behind it — a fact line
    /// that cited nothing, or an id nobody minted, downgraded in code. Read from the
    /// spec's <c>assumptions</c> key.
    /// </summary>
    public IReadOnlyList<string> Assumptions { get; init; } = [];

    /// <summary>
    /// 2026-09-08-1830: the contexts this phase CHANGES, as the derivation declared them
    /// from the list the scope call named. Read from the spec's <c>contexts</c> key; empty
    /// on a spec written before phases named their contexts, which leaves the coverage
    /// check off.
    /// </summary>
    public IReadOnlyList<string> Contexts { get; init; } = [];
}

/// <summary>
/// p0394a: one step of a phase spec — the id the ledger reconciles on, the imperative
/// action, and the spec's target hint (the step's <c>path</c> field, repo-qualified
/// exactly as the spec wrote it) when one is provided.
/// </summary>
public sealed record PhaseStep(string Id, string Action, string? Target);
