using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: the design prompt asks for a work picture until one is stored and renders it once it is; a turn
/// holding an upload reads the uploads first; an allowed turn proposes on 'Ready to propose' or the operator's ask.
/// </summary>
public sealed class WorkPicturePromptTests
{
    [Fact]
    public void WorkPictureSection_NoneStored_AsksForItInTheFirstAnswer()
    {
        var prompt = new SpecDialogPromptFactory().Build(Turns(new SpecDialogTurn("user", "rebuild the legacy app")), 0, 0);

        prompt.Should().Contain("No work picture is stored yet. When the operator asks for work, the first line of "
            + "your answer states it").And.Contain("record it with state_work_picture").And.NotContain("## Work picture");
        prompt.IndexOf("No work picture is stored yet", StringComparison.Ordinal).Should()
            .BeLessThan(prompt.IndexOf("MAY NOT propose", StringComparison.Ordinal), "the contract stays last");
    }

    [Fact]
    public void WorkPictureSection_Stored_RendersValuesAndTheLinesItsKindAndSizeSelect()
    {
        var rebuild = Turns(new SpecDialogTurn("user", "go on"));
        rebuild.Set(ContextKeys.SpecDialogWorkPicture,
            new WorkPicture(UndertakingKind.Rebuild, "the handover upload; repo-a is scaffolding", UndertakingSize.Series));
        var document = Turns(new SpecDialogTurn("user", "go on"));
        document.Set(ContextKeys.SpecDialogWorkPicture, new WorkPicture(UndertakingKind.Document, "repo-a", UndertakingSize.OneSpec));

        WorkPictureSection.Render(rebuild).Should().Contain("## Work picture").And.Contain("- kind: rebuild")
            .And.Contain("- truth: the handover upload; repo-a is scaffolding").And.Contain("- size: series")
            .And.Contain("The operator corrects it in a sentence; record a correction with state_work_picture.")
            .And.Contain("Read the truth in full before you say you are ready; the repositories are compared against it.")
            .And.Contain("You are ready once the truth is read and the cut is agreed with the operator in outline.");
        WorkPictureSection.Render(document).Should().Contain("- size: one-spec")
            .And.Contain("You are ready once the decisions you asked are answered.")
            .And.NotContain("Read the", "a document undertaking selects no reading line");
        new SpecDialogPromptFactory().Build(rebuild, 0, 0).Should().Contain("## Work picture")
            .And.NotContain("No work picture is stored yet");
    }

    [Fact]
    public void DialogReadingLine_TurnWithUploads_PutsUploadsBeforeRepositories()
    {
        var pipeline = Turns(new SpecDialogTurn("user", "what does the upload say?"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["repo-a"] = Mock.Of<ISandbox>(), ["reference:handover"] = Mock.Of<ISandbox>() });

        var prompt = new SpecDialogPromptFactory().Build(pipeline, 0, 0);

        prompt.Should().Contain("The uploads are the subject of this conversation: read them before the repositories")
            .And.Contain("note_reference").And.NotContain("Answer from the code map");
    }

    [Fact]
    public void DialogReadingLine_TurnWithoutUploads_KeepsTheCodeMapLine()
    {
        var pipeline = Turns(new SpecDialogTurn("user", "how does it page?"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["repo-a"] = Mock.Of<ISandbox>(), ["template:base"] = Mock.Of<ISandbox>() });

        DialogReadingLine.Render(pipeline).Should().Be(DialogReadingLine.CodeMap);
        new SpecDialogPromptFactory().Build(pipeline, 0, 0).Should()
            .Contain("Answer from the code map above when it suffices").And.NotContain("The uploads are the subject");
    }

    [Fact]
    public void SpecDialogPromptFactory_MayPropose_NamesReadyToProposeAndOperatorRequest()
    {
        var prompt = new SpecDialogPromptFactory().Build(Turns(
            new SpecDialogTurn("user", "rebuild the legacy app"),
            new SpecDialogTurn("assistant", "Ready to propose: three specs", SpecDialogTurnKind.Answer),
            new SpecDialogTurn("user", "yes")), 0, 0);

        prompt.Should().Contain("MAY propose")
            .And.Contain("when an earlier reply of yours said 'Ready to propose: <the cut in one line>'")
            .And.Contain("the operator's reply to it leaves nothing open, or when the operator asks for it")
            .And.EndWith("keep leading: say 'Ready to propose: <the cut in one line>' once you are, and reply with neither block.");
    }

    private static PipelineContext Turns(params SpecDialogTurn[] transcript)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, transcript);
        return pipeline;
    }
}
