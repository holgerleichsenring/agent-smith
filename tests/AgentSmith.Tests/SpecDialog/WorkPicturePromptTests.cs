using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: the design prompt says whether a work picture is stored and renders it once it is.
/// 2026-11-09-0620k: nothing more — reading, orientation and when to state a picture are the skill's, so the server
/// repeats none of it, and no line orders the reading or keys proposing to a phrase.
/// </summary>
public sealed class WorkPicturePromptTests
{
    [Fact]
    public void WorkPictureSection_NoneStored_SaysOnlyThat()
    {
        var prompt = new SpecDialogPromptFactory().Build(Turns(new SpecDialogTurn("user", "rebuild the legacy app")), 0, 0);

        prompt.Should().Contain("No work picture is stored yet.").And.NotContain("## Work picture")
            .And.NotContain("scaffolding").And.NotContain("When the operator asks for work");
    }

    [Fact]
    public void WorkPictureSection_Stored_RendersItsValues()
    {
        var rebuild = Turns(new SpecDialogTurn("user", "go on"));
        rebuild.Set(ContextKeys.SpecDialogWorkPicture,
            new WorkPicture(UndertakingKind.Rebuild, "the handover upload; repo-a provides auth", UndertakingSize.Series));

        WorkPictureSection.Render(rebuild).Should().Be(
            "## Work picture\n- kind: rebuild\n- truth: the handover upload; repo-a provides auth\n- size: series");
        new SpecDialogPromptFactory().Build(rebuild, 0, 0).Should().Contain("## Work picture")
            .And.NotContain("No work picture is stored yet");
    }

    [Fact]
    public void DesignPrompt_TurnWithUpload_RepeatsNoSkillReadingText()
    {
        var pipeline = Turns(new SpecDialogTurn("user", "what does the upload say?"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["repo-a"] = Mock.Of<ISandbox>(), ["reference:handover"] = Mock.Of<ISandbox>() });

        new SpecDialogPromptFactory().Build(pipeline, 0, 0).Should()
            .NotContain("before the repositories").And.NotContain("The uploads show")
            .And.NotContain("MAY NOT propose").And.NotContain("Ready to propose");
    }

    private static PipelineContext Turns(params SpecDialogTurn[] transcript)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, transcript);
        return pipeline;
    }
}
