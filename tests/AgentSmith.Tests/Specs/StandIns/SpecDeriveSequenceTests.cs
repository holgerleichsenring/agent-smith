using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.StandIns;

/// <summary>2026-11-19-6b0ba: a spec derivation splices the write-only block for each stand-in and nothing for a
/// written spec, which waits for its person; and the stand-in ids round-trip through the series manifest.</summary>
public sealed class SpecDeriveSequenceTests
{
    [Fact]
    public async Task Sequence_UnderSpecDerive_SplicesTheWriteOnlyBlockForStandInsOnly()
    {
        var written = new SpecDraft("2026-11-19-0a0ab", "Written", "spec: 2026-11-19-0a0ab", []);
        var pipeline = Pipeline(PipelinePresets.SpecDeriveName, written);

        var result = await Sequence(pipeline);

        result.InsertNext!.Select(c => (c.Name, c.SpecId)).Should().Equal(
            (CommandNames.SelectSpec, StandInSets.Draft.SpecId), (CommandNames.AgenticMaster, StandInSets.Draft.SpecId),
            (CommandNames.SpecGate, StandInSets.Draft.SpecId));
    }

    [Fact]
    public async Task Sequence_UnderCode_SplicesTheBuildBlockForEverySpec()
    {
        var written = new SpecDraft("2026-11-19-0a0ab", "Written", "spec: 2026-11-19-0a0ab", []);
        var pipeline = Pipeline(PipelinePresets.CodeName, written);

        var result = await Sequence(pipeline);

        result.InsertNext!.Should().HaveCount(2 * PipelinePresets.CodeSpecBlock.Count);
    }

    [Fact]
    public void Manifest_StandIns_RoundTrip()
    {
        var set = StandInSets.Run(new PipelineContext(), StandInSets.Draft).Get<SpecSet>(ContextKeys.SpecSet) with
        {
            Series = "2026-11-19-0a0a",
        };
        var manifest = new AgentSmith.Application.Services.Specs.SeriesManifest();

        manifest.Parse(manifest.Serialize(set))!.StandIns.Should().Equal(StandInSets.Draft.SpecId);
        manifest.Parse(manifest.Serialize(set with { StandIns = [] }))!.StandIns.Should().BeNull();
    }

    private static PipelineContext Pipeline(string pipelineName, SpecDraft written)
    {
        var pipeline = StandInSets.Run(new PipelineContext(), StandInSets.Draft);
        var set = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        pipeline.Set(ContextKeys.SpecSet, set with { Specs = [.. set.Specs, new DerivedSpec(written, "written", string.Empty, [])] });
        pipeline.Set(ContextKeys.PipelineName, pipelineName);
        return pipeline;
    }

    private static Task<AgentSmith.Domain.Models.CommandResult> Sequence(PipelineContext pipeline) =>
        new SpecSequenceHandler(SeriesSettingsDoubles.Reader(null, 1800), TimeProvider.System, NullLogger<SpecSequenceHandler>.Instance)
            .ExecuteAsync(new SpecSequenceContext(pipeline), CancellationToken.None);
}
