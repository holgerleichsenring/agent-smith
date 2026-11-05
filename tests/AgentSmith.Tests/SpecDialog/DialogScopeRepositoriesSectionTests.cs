using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-12-f5f7a: the design dialog is told, before it proposes, that a spec names exactly one
/// of the repositories in scope and that work in two is proposed as an epic.
/// </summary>
public sealed class DialogScopeRepositoriesSectionTests
{
    [Fact]
    public void Render_TwoRepositories_StatesOnePerSpecAndTheEpicSplit()
    {
        var rendered = DialogScopeRepositoriesSection.Render(With("BackgroundWorker", "Server", "template:base"));

        rendered.Should().Contain("## Repositories in scope").And.Contain("EXACTLY ONE")
            .And.Contain("proposed as an epic whose children each name one repository")
            .And.Contain("- BackgroundWorker").And.Contain("- Server").And.NotContain("template:base");
    }

    [Fact]
    public void Render_OneRepositoryOrNone_RendersNothing()
    {
        DialogScopeRepositoriesSection.Render(With("Server")).Should().BeEmpty();
        DialogScopeRepositoriesSection.Render(new PipelineContext()).Should().BeEmpty();
    }

    [Fact]
    public void DesignPrompt_TwoRepositories_CarriesTheSection()
    {
        var pipeline = With("BackgroundWorker", "Server");
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(
            ContextKeys.SpecDialogTranscript, [new SpecDialogTurn("user", "move the retry policy")]);

        new SpecDialogPromptFactory().Build(pipeline, 0, 0)
            .Should().Contain("proposed as an epic whose children each name one repository");
    }

    // 2026-11-05-862ea: a repository may carry several children, so a shared definition is cut ahead of its users.
    [Fact]
    public void DialogScopeRepositoriesSection_TwoRepositories_AllowsSeveralSpecsPerRepository()
    {
        var rendered = DialogScopeRepositoriesSection.Render(With("BackgroundWorker", "Server"));

        rendered.Should().Contain("a repository may carry several children")
            .And.Contain("a definition used across repositories can be its own child ahead of those that use it")
            .And.NotContain("depends on the other second");
    }

    private static PipelineContext With(params string[] keys)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, keys.ToDictionary(k => k, _ => Mock.Of<ISandbox>()));
        return pipeline;
    }
}
