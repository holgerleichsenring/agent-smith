using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.Review.CutRevisionFixtures;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762f: the reviser call — never fatal, a cut reply refused by name, a clean reply admitted.</summary>
public sealed class SpecCutReviserTests
{
    private static readonly SpecReviewCut FlushCut =
        Cut(SpecRegisterNames.Done, "flushes after every single order", "flushes once per batch");

    [Fact]
    public async Task SpecCutReviser_InvalidYaml_ReturnsTheInputDraftAndTheRefusal()
    {
        var draft = Draft();

        var revision = await Revise(Answer(Reply("spec: 2026-10-14-5678a")), draft);

        revision.Revised.Should().BeSameAs(draft);
        revision.Refusal.Should().StartWith("the revised spec is invalid");
    }

    [Fact]
    public async Task SpecCutReviser_CallFails_DoesNotThrow()
    {
        var draft = Draft();

        var revision = await Revise(() => throw new TaskCanceledException("provider timeout"), draft);

        revision.Revised.Should().BeSameAs(draft);
        revision.Refusal.Should().Be("the reviser call failed");
    }

    [Fact]
    public async Task SpecCutReviser_TruncatedReply_IsRefusedAsTruncated()
    {
        var cut = new ChatResponse(new ChatMessage(ChatRole.Assistant, "```yaml\nspec: 2026-10-14-5678a\ngoal: \"The exp"))
        {
            FinishReason = ChatFinishReason.Length,
        };

        var revision = await Revise(() => cut, Draft());

        revision.Refusal.Should().Contain("truncated");
    }

    [Fact]
    public async Task SpecCutReviser_CleanRevision_ReturnsTheRevisedDraft_ResiduesNone()
    {
        var reply = Reply(With("The writer flushes after every single order.", "The writer flushes once per batch."));

        var revision = await Revise(Answer(reply), Draft());

        revision.Refusal.Should().BeNull();
        revision.Revised.Done.Should().Contain("The writer flushes once per batch.");
        new SpecCutResidues().Of(revision.Revised, [FlushCut], []).Should().BeEmpty();
    }

    [Fact]
    public void SpecCutReviser_OutputBound_FollowsTheInputSize()
    {
        var reviser = Reviser(new ScriptedChatClientFactory());
        var large = Draft() with { Yaml = new string('x', 32_718) };

        reviser.MaxOutputTokens(Draft(), new AgentConfig()).Should().Be(4096, "never below the assignment's");
        reviser.MaxOutputTokens(large, new AgentConfig()).Should().Be(32_718 / 3 + 2_000);
    }

    private static Func<ChatResponse> Answer(string text) => () => new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

    private static Task<SpecCutRevision> Revise(Func<ChatResponse> responder, SpecDraft draft) =>
        Reviser(new ScriptedChatClientFactory(responder)).ReviseAsync(draft, [FlushCut], [], lastRound: false,
            new AgentConfig(), PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None);

    private static SpecCutReviser Reviser(ScriptedChatClientFactory factory) =>
        new(factory, new AsyncLocalRunContextAccessor(), new SpecDraftValidator(new SpecSchemaProvider()),
            new SpecRevisionGuard(), new SpecDraftReader(), NullLogger<SpecCutReviser>.Instance);
}
