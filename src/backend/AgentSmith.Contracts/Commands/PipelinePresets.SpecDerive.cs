using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-10-26-bdd0b: true for a run that carries a ticket id but never moves the ticket: no working
    /// or failure comment, no status, no lifecycle label, no queue entry, no taken-ticket record.</summary>
    public static bool LeavesTicketAlone(string? pipelineName) =>
        string.Equals(pipelineName, SpecDeriveName, StringComparison.OrdinalIgnoreCase) || ShownOnTicketOnly(pipelineName)
        || string.Equals(pipelineName, MergeGreenName, StringComparison.OrdinalIgnoreCase); // 2026-11-17-3435b: merges, moves nothing

    /// <summary>2026-11-17-b34ed: a run that acts beside a ticket — on its pull request or branch — and is shown on its
    /// page: its row carries the ticket's id (PipelineRequest.ShownOnTicket), it leaves the ticket alone and nudges no
    /// rework when it ends.</summary>
    public static bool ShownOnTicketOnly(string? pipelineName) =>
        string.Equals(pipelineName, RefreshBaseName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(pipelineName, RetargetName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(pipelineName, DeployName, StringComparison.OrdinalIgnoreCase); // 2026-11-18-3863c

    /// <summary>2026-11-11-205ff review: true for a run that something relaunches when it did not fit — a ticket's run,
    /// which the reconciler re-enqueues. A derivation and a design turn are started by a person; nothing relaunches
    /// them, so a capacity refusal ends them failed instead of 'queued'.</summary>
    public static bool WaitsForCapacity(string? pipelineName) =>
        !string.Equals(pipelineName, SpecDeriveName, StringComparison.OrdinalIgnoreCase) // b34ed: a refresh still waits
        && !(pipelineName is not null && NeedsHostContext(pipelineName));

    /// <summary>The same question asked of a running pipeline, by the name the run was started under.</summary>
    public static bool LeavesTicketAlone(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.PipelineName, out var name) && LeavesTicketAlone(name);

    /// <summary>2026-10-26-bdd0a: preset name of the spec derivation, the act that derives a ticket's specs
    /// without attempting its work.</summary>
    public const string SpecDeriveName = "spec-derive";

    // 2026-10-26-bdd0a: the code preset's front, word for word, up to and including SpecGate; 2026-11-19-6b0ba: then
    // the coding master on each stand-in, writing the spec only — no prerequisites, no commit, no pull request of code. The derivation and the per-repository placement are the code run's own, so a set derived here is the set a code
    // run would have derived. Written out rather than taken from Code: a field initializer in a partial file
    // runs in an order the C# spec does not fix. No PipelineNameInitializer: the name is not in the catalog's
    // pipeline_name enum and no skill of the front keys on it, the reason spec-dialog leaves the step out.
    public static readonly IReadOnlyList<string> SpecDerive =
    [
        CommandNames.LoadCatalog,
        CommandNames.FetchTicket,
        CommandNames.ScopeRepos,
        CommandNames.RunConfigPreflight, // 2026-11-16-f3fba
        CommandNames.CheckoutSource,
        CommandNames.RunPreflight,
        CommandNames.SetupRegistryAuth,
        CommandNames.LoadCodingPrinciples, CommandNames.LoadMemoryIndex,
        CommandNames.LoadDesignSystem,
        CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        CommandNames.DeriveSpec,
        CommandNames.SpecHandback,
        CommandNames.SpecGate,
        // 2026-11-19-6b0ba: the coding master writes each stand-in — the uploads it builds against first.
        CommandNames.MaterializeReferenceSets,
        CommandNames.SpecSequence,
    ];

    /// <summary>2026-11-19-6b0ba: what a spec derivation splices per stand-in — the coding master writes the spec and
    /// builds nothing; the gate after it judges that one spec and ends the run when none is left.</summary>
    public static readonly IReadOnlyList<string> SpecDeriveBlock =
    [
        CommandNames.SelectSpec,
        CommandNames.AgenticMaster,
        CommandNames.SpecGate,
    ];

    /// <summary>The block SpecSequence splices per spec for <paramref name="pipelineName"/>.</summary>
    public static IReadOnlyList<string> SpecBlockFor(string? pipelineName) =>
        string.Equals(pipelineName, SpecDeriveName, StringComparison.OrdinalIgnoreCase) ? SpecDeriveBlock : CodeSpecBlock;
}
