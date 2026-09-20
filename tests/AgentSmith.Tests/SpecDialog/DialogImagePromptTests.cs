using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Tests.Handlers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-20-3af8: what a design turn hands the design partner. The seam at the end of the
/// path was already unconditional — the master feeds user image parts into every loop request
/// and only the spec-dialog arm substituted an empty list for them — so these run the REAL
/// handler and read what the loop was actually asked for.
/// </summary>
public sealed class DialogImagePromptTests
{
    private const string Master = "design-partner-master";

    [Fact]
    public async Task Dialog_AnImage_RidesTheNextMasterCallAsImageContent()
    {
        var loop = new CapturingLoop();

        await RunAsync(loop, Images(1));

        var parts = loop.Seen[0].UserImageParts;
        parts.Should().NotBeNull().And.ContainSingle();
        var image = parts![0].Should().BeOfType<DataContent>().Subject;
        image.MediaType.Should().Be("image/png");
        loop.Seen[0].UserPrompt.Should().Contain(
            "1 image(s) from this conversation are attached to this message as image content.");
    }

    [Fact]
    public async Task Dialog_ANonVisionAgent_IsToldTheImagesExist()
    {
        var loop = new CapturingLoop();

        await RunAsync(loop, Images(2), supportsVision: false);

        loop.Seen[0].UserImageParts.Should().BeNullOrEmpty(
            "a model that cannot see an image is handed none");
        loop.Seen[0].UserPrompt.Should().Contain("2 image(s)").And.Contain("NOT viewable",
            "the operator's screenshot is never dropped in silence");
    }

    [Fact]
    public async Task Dialog_MoreImagesThanTheCeiling_CarriesTheMostRecent()
    {
        var loop = new CapturingLoop();

        await RunAsync(loop, Images(DialogImageParts.MaxImages + 2));

        var parts = loop.Seen[0].UserImageParts;
        parts.Should().HaveCount(DialogImageParts.MaxImages,
            "a conversation re-sends its whole prompt every turn, so the ceiling bounds a TURN");
        parts!.Select(part => ((DataContent)part).Data.ToArray()[0])
            .Should().Equal([3, 4, 5, 6], "the most recent, because that is what the question is about");
        DialogImageParts.MaxImages.Should().Be(4,
            "four is what a person attaches to one question; ten bounds a ticket read once in a run");
    }

    [Fact]
    public async Task Dialog_MoreImagesThanTheCeiling_SaysHowManyExistAndHowManyAreCarried()
    {
        var loop = new CapturingLoop();

        await RunAsync(loop, Images(6));

        loop.Seen[0].UserPrompt.Should()
            .Contain("6 image(s) are attached to this conversation; the 4 most recent",
                "an operator whose early diagram dropped out is told it did");
    }

    [Fact]
    public async Task Dialog_AConversationWithNoImages_SaysNothingAboutThem()
    {
        var loop = new CapturingLoop();

        await RunAsync(loop, DialogImageSet.None);

        loop.Seen[0].UserImageParts.Should().BeNullOrEmpty();
        loop.Seen[0].UserPrompt.Should().NotContain("image(s)");
    }

    /// <summary>The set the loader would have built: <paramref name="count"/> images, oldest
    /// first, each one recognisable by its first byte.</summary>
    private static DialogImageSet Images(int count) =>
        new(count, [.. Enumerable.Range(1, count).Select(n => new DialogImage("image/png", [(byte)n]))]);

    private static async Task RunAsync(
        CapturingLoop loop, DialogImageSet images, bool supportsVision = true)
    {
        var context = MasterHandlerFixture.BuildContext(
            Master, includeTicket: false, supportsVision: supportsVision);
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(
            ContextKeys.SpecDialogTranscript, [new SpecDialogTurn("user", "what is on this screen?")]);
        context.Pipeline.Set(ContextKeys.SpecDialogImages, images);

        await MasterHandlerFixture
            .Build(loop, new MasterHandlerFixture.StubPromptCatalog(Master, "body"))
            .ExecuteAsync(context, CancellationToken.None);
    }

    private sealed class CapturingLoop : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];

        public IReadOnlyList<AgenticLoopRequest> Seen => _seen;

        public Task<AgenticLoopResult> RunAsync(
            AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            _seen.Add(request);
            return Task.FromResult(new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "that is the settings pane")),
                TimeSpan.FromSeconds(1)));
        }
    }
}
