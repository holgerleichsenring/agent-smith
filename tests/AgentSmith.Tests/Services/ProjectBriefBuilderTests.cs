using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-04-cf3d: the scan brief parses each context.yaml on its own — a sandbox may hold
/// several, and a labelled concatenation of two YAML documents is not one.
/// </summary>
public sealed class ProjectBriefBuilderTests
{
    [Fact]
    public void Build_OneContext_RendersItsRelevantKeysUnlabelled()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<ContextDocument>>(ContextKeys.RepoContextYamls,
            [Doc("default", "meta:\n  project: sample\nstate:\n  done: {}")]);

        var brief = new ProjectBriefBuilder().Build(pipeline);

        brief.Should().Be("## Project Brief\n\n### meta\n- project: sample");
    }

    [Fact]
    public void Build_TwoContextsInOneSandbox_RendersEachUnderItsContext()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<ContextDocument>>(ContextKeys.RepoContextYamls,
            [Doc("backend", "stack:\n  lang: csharp"), Doc("frontend", "stack:\n  lang: typescript")]);

        var brief = new ProjectBriefBuilder().Build(pipeline);

        brief.Should().Contain("### context — backend\n\n### stack\n- lang: csharp")
            .And.Contain("### context — frontend\n\n### stack\n- lang: typescript");
    }

    [Fact]
    public void Build_NothingLoaded_SaysSo()
    {
        new ProjectBriefBuilder().Build(new PipelineContext())
            .Should().Be("## Project Brief\nStack: unknown — review on source-snippets only.");
    }

    private static ContextDocument Doc(string context, string yaml) =>
        new("primary", context, context, $"/work/.agentsmith/contexts/{context}/context.yaml", yaml);
}
