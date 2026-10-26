using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-10-26-bdd0b: true for a run that carries a ticket id but never moves the ticket: no working
    /// or failure comment, no status, no lifecycle label, no queue entry, no taken-ticket record.</summary>
    public static bool LeavesTicketAlone(string? pipelineName) =>
        string.Equals(pipelineName, SpecDeriveName, StringComparison.OrdinalIgnoreCase);

    /// <summary>The same question asked of a running pipeline, by the name the run was started under.</summary>
    public static bool LeavesTicketAlone(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.PipelineName, out var name) && LeavesTicketAlone(name);

    /// <summary>2026-10-26-bdd0a: preset name of the spec derivation, the act that derives a ticket's specs
    /// without attempting its work.</summary>
    public const string SpecDeriveName = "spec-derive";

    // 2026-10-26-bdd0a: the code preset's front, word for word, up to and including SpecGate, and nothing
    // after it: no prerequisites, no master, no commit, no pull request of code. The derivation, the review
    // rounds and the per-repository placement are the code run's own, so a set derived here is the set a code
    // run would have derived. Written out rather than taken from Code: a field initializer in a partial file
    // runs in an order the C# spec does not fix. No PipelineNameInitializer: the name is not in the catalog's
    // pipeline_name enum and no skill of the front keys on it, the reason spec-dialog leaves the step out.
    public static readonly IReadOnlyList<string> SpecDerive =
    [
        CommandNames.LoadCatalog,
        CommandNames.FetchTicket,
        CommandNames.ScopeRepos,
        CommandNames.CheckoutSource,
        CommandNames.RunPreflight,
        CommandNames.SetupRegistryAuth,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate,
        CommandNames.LoadCodingPrinciples, CommandNames.LoadMemoryIndex,
        CommandNames.LoadDesignSystem,
        CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        CommandNames.DeriveSpec,
        CommandNames.SpecHandback,
        CommandNames.ReviewSpecs,
        CommandNames.SpecGate,
    ];
}
