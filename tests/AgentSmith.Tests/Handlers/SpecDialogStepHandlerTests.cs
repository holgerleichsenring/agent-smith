using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0315b: the two spec-dialog preset steps — cached code map publication
/// (tier-1 grounding, misses reported inline) and the reply hand-back slot.
/// </summary>
public sealed class SpecDialogStepHandlerTests
{
    [Fact]
    public async Task LoadCachedCodeMap_MixOfHitAndMiss_PublishesBothInline()
    {
        var store = new Mock<IProjectMapStore>();
        store.Setup(s => s.ListByPrefixAsync("repo-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProjectMap(
                "csharp", [], [new Module("src", ModuleRole.Production, [])], [], [],
                new Conventions(null, null, null), new CiConfig(false, null, null, null))]);
        store.Setup(s => s.ListByPrefixAsync("repo-b", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "repo-a" }, new RepoConnection { Name = "repo-b" }]);
        var sut = new LoadCachedCodeMapHandler(store.Object, NullLogger<LoadCachedCodeMapHandler>.Instance);

        var result = await sut.ExecuteAsync(new LoadCachedCodeMapContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var codeMap = pipeline.Get<string>(ContextKeys.CodeMap);
        codeMap.Should().Contain("### repo-a").And.Contain("primary_language: csharp");
        codeMap.Should().Contain("### repo-b").And.Contain("no cached code map",
            "a miss is reported inline so the master escalates instead of guessing");
    }

    [Fact]
    public async Task CollectSpecDialogReply_CopiesMasterAnswerIntoSlot()
    {
        var pipeline = new PipelineContext();
        var slot = new SpecDialogReplySlot();
        pipeline.Set(ContextKeys.SpecDialogReplySlot, slot);
        pipeline.Set(ContextKeys.MasterAnswer, "the grounded answer");
        // p0315e: the typed terminal outcome travels with the reply.
        pipeline.Set(ContextKeys.SpecDialogOutcome, (OutcomeProposal)new AnswerOutcome());
        var sut = new CollectSpecDialogReplyHandler();

        var result = await sut.ExecuteAsync(new CollectSpecDialogReplyContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        slot.Reply.Should().Be("the grounded answer");
        slot.Outcome.Should().BeOfType<AnswerOutcome>();
    }

    [Fact]
    public async Task CollectSpecDialogReply_MissingOutcome_FailsLoud()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogReplySlot, new SpecDialogReplySlot());
        pipeline.Set(ContextKeys.MasterAnswer, "answer");
        var sut = new CollectSpecDialogReplyHandler();

        var result = await sut.ExecuteAsync(new CollectSpecDialogReplyContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse("an absent proposal is a composition bug, not an answer");
        result.Message.Should().Contain("outcome");
    }

    [Fact]
    public async Task CollectSpecDialogReply_MissingSlot_FailsLoud()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.MasterAnswer, "answer");
        var sut = new CollectSpecDialogReplyHandler();

        var result = await sut.ExecuteAsync(new CollectSpecDialogReplyContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("reply slot");
    }

    [Fact]
    public async Task CollectSpecDialogReply_EmptyAnswer_FailsLoud()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogReplySlot, new SpecDialogReplySlot());
        var sut = new CollectSpecDialogReplyHandler();

        var result = await sut.ExecuteAsync(new CollectSpecDialogReplyContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("no reply text");
    }
}
