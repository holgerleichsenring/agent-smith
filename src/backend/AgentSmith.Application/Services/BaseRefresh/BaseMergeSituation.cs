using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: what one pass of the base-merge master works on — the work branch and the base, the spec
/// files each side added since they parted, and either the paths still unmerged or the stage the merged tree turned red.</summary>
public sealed record BaseMergeSituation(
    ResolvedProject Project, PipelineContext Pipeline, ISandbox Sandbox, string Branch, string BaseRef,
    IReadOnlyList<string> OurSpecs, IReadOnlyList<string> TheirSpecs)
{
    public IReadOnlyList<string> Unmerged { get; init; } = [];

    public VerifyOutcome? Red { get; init; }
}
