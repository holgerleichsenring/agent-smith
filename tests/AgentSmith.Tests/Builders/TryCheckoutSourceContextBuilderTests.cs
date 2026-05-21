using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Builders;

public sealed class TryCheckoutSourceContextBuilderTests
{
    private readonly TryCheckoutSourceContextBuilder _builder = new();

    [Fact]
    public void Build_SourceConfigDefaultBranchSet_PassesAsBranchName()
    {
        var repo = new RepoConnection { Type = RepoType.GitHub, Url = "u", DefaultBranch = "develop" };
        var project = new ResolvedProject { Repos = new[] { repo } };
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { repo });

        var context = (TryCheckoutSourceContext)_builder.Build(
            new PipelineCommand(CommandNames.TryCheckoutSource), project, pipeline);

        context.Branch.Should().Be(new BranchName("develop"));
    }

    [Fact]
    public void Build_SourceConfigDefaultBranchUnset_PassesNullBranch()
    {
        var repo = new RepoConnection { Type = RepoType.GitHub, Url = "u", DefaultBranch = null };
        var project = new ResolvedProject { Repos = new[] { repo } };
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { repo });

        var context = (TryCheckoutSourceContext)_builder.Build(
            new PipelineCommand(CommandNames.TryCheckoutSource), project, pipeline);

        context.Branch.Should().BeNull();
    }

    [Fact]
    public void Build_IgnoresContextKeysCheckoutBranchAndTicketId()
    {
        var repo = new RepoConnection { Type = RepoType.GitHub, Url = "u", DefaultBranch = null };
        var project = new ResolvedProject { Repos = new[] { repo } };
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, new[] { repo });
        pipeline.Set(ContextKeys.CheckoutBranch, "feature/from-webhook");
        pipeline.Set(ContextKeys.TicketId, new TicketId("BUG-123"));

        var context = (TryCheckoutSourceContext)_builder.Build(
            new PipelineCommand(CommandNames.TryCheckoutSource), project, pipeline);

        context.Branch.Should().BeNull();
    }
}
