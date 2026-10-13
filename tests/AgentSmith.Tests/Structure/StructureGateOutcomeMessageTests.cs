using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-13-2d0c: what the commit step's message says — the verdict first, the formatter note
/// after it, and how much the gate judged, or that it judged nothing and why.
/// </summary>
public sealed class StructureGateOutcomeMessageTests
{
    private const string FormatNote = "format: the api formatter exited 127 in repo; its 1 file(s) are committed as written";
    private static readonly StructureGateVerdict Found = new(
        [new StructureFinding("src/A.cs", "A.Long()", StructureMetric.FunctionLines, 70, 55)]);

    private static StructureGateOutcome Outcome() =>
        new(TestSpecReview.Revert(), new Mock<ISpecProgressRecorder>().Object);

    private static PipelineContext Spec()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("2026-10-13-test", "goal", "spec: x\n", []));
        return pipeline;
    }

    [Fact]
    public async Task StructureGateOutcome_FormatterNote_FollowsTheVerdict()
    {
        var pipeline = Spec();
        pipeline.Set(ContextKeys.StructureGateAttempted, true);

        var result = await Outcome().ResolveAsync(pipeline, Found, FormatNote, CancellationToken.None);

        result.Message.Split('\n')[0].Should().StartWith("structure gate: 1 unit(s) over their Limits");
        result.Message.Should().EndWith(FormatNote);
    }

    [Fact]
    public async Task StructureGateOutcome_FormatterNoteInFixPass_RevertReasonIsTheFinding()
    {
        var pipeline = Spec();
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.VerifiedHeads,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["repo"] = "verified-sha" });
        SpecReviewLedger.Record(pipeline, SpecReviewReport.Taken([new SpecFinding("repo", "src/A.cs", 4, "rule", "why", "P1")]));
        SpecReviewFixPass.Prepare(pipeline);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["repo"] = new StubSandbox() });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>());

        await Outcome().ResolveAsync(pipeline, Found, FormatNote, CancellationToken.None);

        pipeline.Get<string>(ContextKeys.SpecReviewReverted).Should().Contain("structure gate").And.NotContain("format:");
    }

    [Fact]
    public async Task StructureGateOutcome_NothingJudged_SaysSo()
    {
        var verdict = StructureGateVerdict.NotJudged("repo: no context with Limits and a baseline at the merge-base with origin/main");

        var result = await Outcome().ResolveAsync(Spec(), verdict, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Spec work is on the branch; structure not judged: "
            + "repo: no context with Limits and a baseline at the merge-base with origin/main");
    }

    [Fact]
    public async Task StructureGateOutcome_Judged_NamesFilesAndContexts()
    {
        var verdict = new StructureGateVerdict([]) { JudgedFiles = 3, JudgedContexts = 2 };

        var result = await Outcome().ResolveAsync(Spec(), verdict, null, CancellationToken.None);

        result.Message.Should().Be("Spec work is on the branch; structure judged 3 file(s) in 2 context(s), within their Limits");
    }
}
