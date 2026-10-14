using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>2026-10-13-8762n: a spec entry that re-grounds nothing — the spec enters as it was handed in.</summary>
internal sealed class PassThroughEntryGrounding : ISpecEntryGrounding
{
    public Task<SpecEntryGround> GroundAsync(PipelineContext pipeline, SpecSet set, DerivedSpec spec, CancellationToken ct) =>
        Task.FromResult(new SpecEntryGround(spec, string.Empty));
}
