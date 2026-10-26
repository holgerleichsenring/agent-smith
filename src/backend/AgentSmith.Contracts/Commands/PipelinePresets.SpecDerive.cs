namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
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
