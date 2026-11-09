using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-05-862ea: the design prompt asks for a work picture until one is stored and renders it once it is.
/// 2026-11-09-0620b: a turn holding an upload is told what each source shows, with no reading order; the repositories
/// are what exists, not scaffolding; no readiness line keys proposing to a phrase.
/// </summary>
public sealed class WorkPicturePromptTests
{
    [Fact]
    public void WorkPictureSection_NoneStored_AsksForItInTheFirstAnswer()
    {
        var prompt = new SpecDialogPromptFactory().Build(Turns(new SpecDialogTurn("user", "rebuild the legacy app")), 0, 0);

        prompt.Should().Contain("No work picture is stored yet. When the operator asks for work, the first line of "
            + "your answer states it").And.Contain("record it with state_work_picture").And.NotContain("## Work picture")
            .And.Contain("what the repositories already provide").And.NotContain("scaffolding");
        prompt.IndexOf("No work picture is stored yet", StringComparison.Ordinal).Should()
            .BeLessThan(prompt.IndexOf("Propose the work once", StringComparison.Ordinal), "the contract stays last");
    }

    [Fact]
    public void WorkPictureSection_Stored_RendersValuesAndTheLineItsKindSelects()
    {
        var rebuild = Turns(new SpecDialogTurn("user", "go on"));
        rebuild.Set(ContextKeys.SpecDialogWorkPicture,
            new WorkPicture(UndertakingKind.Rebuild, "the handover upload; repo-a provides auth", UndertakingSize.Series));
        var document = Turns(new SpecDialogTurn("user", "go on"));
        document.Set(ContextKeys.SpecDialogWorkPicture, new WorkPicture(UndertakingKind.Document, "repo-a", UndertakingSize.OneSpec));

        WorkPictureSection.Render(rebuild).Should().Contain("## Work picture").And.Contain("- kind: rebuild")
            .And.Contain("- truth: the handover upload; repo-a provides auth").And.Contain("- size: series")
            .And.Contain("The operator corrects it in a sentence; record a correction with state_work_picture.")
            .And.Contain("A rebuild carries over everything the source does").And.NotContain("You are ready once");
        WorkPictureSection.Render(document).Should().Contain("- size: one-spec")
            .And.NotContain("A rebuild").And.NotContain("You are ready once", "a document undertaking selects no line");
        new SpecDialogPromptFactory().Build(rebuild, 0, 0).Should().Contain("## Work picture")
            .And.NotContain("No work picture is stored yet");
    }

    [Fact]
    public void DialogReadingLine_TurnWithUpload_DescribesBothSourcesWithoutAnOrder()
    {
        var pipeline = Turns(new SpecDialogTurn("user", "what does the upload say?"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["repo-a"] = Mock.Of<ISandbox>(), ["reference:handover"] = Mock.Of<ISandbox>() });

        var prompt = new SpecDialogPromptFactory().Build(pipeline, 0, 0);

        prompt.Should().Contain("The uploads show what the operator wants built")
            .And.Contain("The repositories show what already exists and what it already decides")
            .And.Contain("note_reference").And.NotContain("before the repositories").And.NotContain("The code map above says");
    }

    [Fact]
    public void DialogReadingLine_TurnWithoutUploads_KeepsTheCodeMapLine()
    {
        var pipeline = Turns(new SpecDialogTurn("user", "how does it page?"));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["repo-a"] = Mock.Of<ISandbox>(), ["template:base"] = Mock.Of<ISandbox>() });

        DialogReadingLine.Render(pipeline).Should().Be(DialogReadingLine.CodeMap);
        new SpecDialogPromptFactory().Build(pipeline, 0, 0).Should()
            .Contain("The code map above says where things live").And.NotContain("The uploads show");
    }

    private static PipelineContext Turns(params SpecDialogTurn[] transcript)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, transcript);
        return pipeline;
    }
}
