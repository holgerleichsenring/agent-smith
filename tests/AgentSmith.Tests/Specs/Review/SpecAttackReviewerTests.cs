using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.Review.AttackReviewFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762e: the attack reviewer's call — not taken without a look, never fatal, and its
/// answer admitted before it is returned.</summary>
public sealed class SpecAttackReviewerTests
{
    [Fact]
    public async Task SpecAttackReviewer_NoLook_IsNotTaken()
    {
        var factory = new ScriptedChatClientFactory();

        var result = await Reviewer(factory).ReviewAsync(Input(), look: null, new AgentConfig(),
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

        result.Should().BeNull();
        factory.ResponseCounter.Should().BeEmpty("nothing to attack with is no call at all");
    }

    [Fact]
    public async Task SpecAttackReviewer_CallFails_ReturnsNothingAndDoesNotThrow()
    {
        var factory = new ScriptedChatClientFactory(() => throw new TaskCanceledException("provider timeout"));

        var act = () => Reviewer(factory).ReviewAsync(Input(), Look(), new AgentConfig(),
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

        (await act.Should().NotThrowAsync()).Subject.Should().BeNull();
    }

    [Fact]
    public async Task SpecAttackReviewer_ReadableAnswer_IsAdmittedBeforeItIsReturned()
    {
        var factory = new ScriptedChatClientFactory(() => new ChatResponse(new ChatMessage(ChatRole.Assistant,
            """{"verdict": "REFUSE", "cuts": [], "findings": [{"text": "made up", "repository": "x", "path": "y", "line": 1, "cites": "A1"}]}""")));

        var result = await Reviewer(factory).ReviewAsync(Input(), Look(), new AgentConfig(),
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

        result!.Verdict.Should().Be(SpecReviewVerdicts.Build, "a refuse resting on a look nobody took is no refuse");
        result.Findings.Should().BeEmpty();
    }

    private const string BuildAnswer = """{"verdict": "BUILD", "cuts": [], "findings": []}""";
    private const string RefuseAnswer =
        """{"verdict": "REFUSE", "cuts": [], "findings": [{"text": "made up", "repository": "x", "path": "y", "cites": "A9"}]}""";

    [Fact]
    public async Task SpecAttackReviewer_EveryLookFailed_IsBlindWhateverItSaid()
    {
        var result = await AnsweredAsync(BuildAnswer, false, false);

        result!.Blind.Should().BeTrue();
    }

    [Fact]
    public async Task SpecAttackReviewer_RefuseWithoutFindingAndAFailedLook_IsBlind()
    {
        var result = await AnsweredAsync(RefuseAnswer, true, false);

        result!.Blind.Should().BeTrue("that refuse may rest on what it failed to read");
    }

    [Fact]
    public async Task SpecAttackReviewer_RefuseWithoutFindingAndAllLooksRan_StaysBuild()
    {
        var result = await AnsweredAsync(RefuseAnswer, true, true);

        result!.Blind.Should().BeFalse();
        result.Verdict.Should().Be(SpecReviewVerdicts.Build);
    }

    [Fact]
    public async Task SpecAttackReviewer_PartReadAndBuild_StaysWhatItSaid()
    {
        var result = await AnsweredAsync(BuildAnswer, true, false);

        result!.Blind.Should().BeFalse();
    }

    [Fact]
    public async Task SpecAttackReviewer_NoLookAtAll_IsNotBlind()
    {
        var result = await AnsweredAsync(BuildAnswer);

        result!.Blind.Should().BeFalse("one who opened nothing never tried");
    }

    private static Task<SpecAttackResult?> AnsweredAsync(string answer, params bool[] ran)
    {
        var look = Look();
        foreach (var one in ran)
            look.Evidence.Remember(new EvidenceRecord(Key, EvidenceRecord.Read, "read a.cs", one ? 0 : 2, Ran: one, "a.cs"));
        var factory = new ScriptedChatClientFactory(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        return Reviewer(factory).ReviewAsync(Input(), look, new AgentConfig(),
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);
    }

    private static SpecAttackReviewer Reviewer(ScriptedChatClientFactory factory) =>
        new(factory, new AsyncLocalRunContextAccessor(), new SpecAttackAdmission(NullLogger<SpecAttackAdmission>.Instance),
            NullLogger<SpecAttackReviewer>.Instance);
}
