using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.Integration;
using AgentSmith.Tests.Specs.MultiRepo;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-12-f5f7f fast-tier, real git: the publish as the server composes it, into two repositories
/// whose second refuses the push. Both stay on the previous revision, the step fails with the reason,
/// and the next run reads the series as this system's own — no reviewer edit.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesWriteRollbackTests
{
    [Fact]
    public async Task PushRefusedInClient_BothStayOnPreviousRevision_NextRunSeesNoEdit()
    {
        await using var repos = await TwoRepoSeries.CreateAsync();
        if (repos is null) return;
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default));
        var publisher = harness.Services.GetRequiredService<ISpecSetPublisher>();
        var scope = new SeriesScope([TwoRepoSeries.Server, TwoRepoSeries.Client], null, [], null);
        (await publisher.PublishAsync(Pipeline(repos), "p", scope, SeriesWriteRollbackGitTests.Revision1(), [], default))
            .IsSuccess.Should().BeTrue();
        repos.Hook("Client", "pre-receive", "exit 1");

        var failed = await publisher.PublishAsync(Pipeline(repos), "p", scope, SeriesWriteRollbackGitTests.Revision2(), [], default);
        await repos.CheckOutRemoteAsync("Server");
        await repos.CheckOutRemoteAsync("Client");
        var next = await harness.Services.GetRequiredService<ISpecSetReader>().ReadAsync(Pipeline(repos),
            TwoRepoSeries.Server, [TwoRepoSeries.Server, TwoRepoSeries.Client], new TicketKey(SeriesRepoBranches.Key), default);
        var pointer = await harness.Services.GetRequiredService<ISpecSetPointerStore>().GetAsync("p", SeriesRepoBranches.Key, default);

        failed.IsSuccess.Should().BeFalse();
        failed.Message.Should().Contain("Client").And.Contain("reverted in Server");
        next.Set!.Current.Number.Should().Be(1, "both repositories are on the previous revision");
        next.Read!.Lagging.Should().BeEmpty();
        SpecRevisionCause.For(next.Read, pointer, Ticket(), new PipelineContext())
            .Should().Be(SpecRevisionCause.Retrigger, "our own revert is no reviewer's edit");
    }

    private static PipelineContext Pipeline(TwoRepoSeries repos)
    {
        var pipeline = repos.Pipeline();
        pipeline.Set(ContextKeys.ProjectName, "p");
        return pipeline;
    }

    private static Ticket Ticket() => new(new TicketId("4711"), "t", "text", null, "open", "azdo", []);
}
