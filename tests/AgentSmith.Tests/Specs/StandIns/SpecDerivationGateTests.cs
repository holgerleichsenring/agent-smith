using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.StandIns;

/// <summary>2026-11-19-6b0ba: in a spec derivation the gate lets the sequence run while a stand-in waits, judges the
/// spec of the master before it, and ends the run on where its specs are once none is left.</summary>
public sealed class SpecDerivationGateTests
{
    [Fact]
    public void Front_AStandInWaits_LetsTheSequenceRun()
    {
        var pipeline = StandInSets.Run(new PipelineContext(), StandInSets.Draft);

        SpecDerivationGate.Front(pipeline, Set(pipeline)).Should().BeNull();
        pipeline.Has(ContextKeys.SpecDerivationEnded).Should().BeFalse();
    }

    [Fact]
    public void Front_NoStandIn_EndsTheRunOnItsSentence()
    {
        var pipeline = StandInSets.Run(new PipelineContext(), StandInSets.Draft, standIn: false);
        pipeline.Set(ContextKeys.SpecDerivationPreview, true);

        SpecDerivationGate.Front(pipeline, Set(pipeline))!.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.SpecDerivationEnded).Should().Contain("as a preview");
    }

    [Fact]
    public void AfterMaster_StillAStandIn_FailsNamingIt()
    {
        var pipeline = StandInSets.Run(new PipelineContext(), StandInSets.Draft);

        var result = SpecDerivationGate.AfterMaster(pipeline, Set(pipeline), StandInSets.Draft.SpecId);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(StandInSets.Draft.SpecId).And.Contain("wrote no spec");
    }

    [Fact]
    public void AfterMaster_ABlocker_BecomesTheQuestionTheHandbackStepAsks()
    {
        var pipeline = StandInSets.Run(new PipelineContext(), StandInSets.Draft);
        pipeline.Set(ContextKeys.MasterVerification, new MasterVerification(
            VerificationStatus.Failed, false, false, false, false, "blocked", Blocker: "Which client is meant?"));

        var result = SpecDerivationGate.AfterMaster(pipeline, Set(pipeline), StandInSets.Draft.SpecId);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback);
        pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Should().Be(
            new SpecHandback(SpecHandbackCase.Question, "Which client is meant?"));
    }

    [Fact]
    public void AfterMaster_WrittenAndNoneLeft_EndsTheRun()
    {
        var pipeline = StandInSets.Run(new PipelineContext(), StandInSets.Draft, standIn: false);
        pipeline.Set(ContextKeys.SpecRevisionSha, "sha");

        var result = SpecDerivationGate.AfterMaster(pipeline, Set(pipeline), StandInSets.Draft.SpecId);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.SpecDerivationEnded).Should().Contain("stored on the ticket branch");
    }

    private static SpecSet Set(PipelineContext pipeline) => pipeline.Get<SpecSet>(ContextKeys.SpecSet);
}
