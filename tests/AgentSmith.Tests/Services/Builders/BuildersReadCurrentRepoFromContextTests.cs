using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Builders;

/// <summary>
/// p0140d: context builders that previously read project.Repo now read the per-run
/// RepoConnection from PipelineContext under ContextKeys.CurrentRepo. These tests
/// pin that contract so a regression to project.Repo (or a typo on the key) trips
/// before reaching production.
/// </summary>
public sealed class BuildersReadCurrentRepoFromContextTests
{
    private static readonly RepoConnection ExpectedRepo = new()
    {
        Name = "the-repo",
        Url = "https://example.com/the-repo",
        Type = RepoType.GitHub,
    };

    private static readonly ResolvedProject ProjectWithDifferentSibling = new()
    {
        Name = "multi",
        Repos = new[]
        {
            ExpectedRepo,
            new RepoConnection { Name = "sibling", Url = "https://example.com/sibling" }
        }
    };

    [Fact]
    public void CommitAndPRContextBuilder_ReadsCurrentRepoFromPipelineContext()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentRepo, ExpectedRepo);
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "url"));
        pipeline.Set<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges, Array.Empty<CodeChange>());
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            new TicketId("1"), "title", "desc", null, "Open", "GitHub"));

        var ctx = (CommitAndPRContext)new CommitAndPRContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.CommitAndPR), ProjectWithDifferentSibling, pipeline);

        ctx.RepoConnection.Should().BeSameAs(ExpectedRepo);
    }

    [Fact]
    public void InitCommitContextBuilder_ReadsCurrentRepoFromPipelineContext()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentRepo, ExpectedRepo);
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("agentsmith/init"), "url"));

        var ctx = (InitCommitContext)new InitCommitContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.InitCommit), ProjectWithDifferentSibling, pipeline);

        ctx.RepoConnection.Should().BeSameAs(ExpectedRepo);
    }

    [Fact]
    public void AcquireSourceContextBuilder_ReadsCurrentRepoFromPipelineContext()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CurrentRepo, ExpectedRepo);

        var ctx = (AcquireSourceContext)new AcquireSourceContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.AcquireSource), ProjectWithDifferentSibling, pipeline);

        ctx.Config.Should().BeSameAs(ExpectedRepo);
    }
}
