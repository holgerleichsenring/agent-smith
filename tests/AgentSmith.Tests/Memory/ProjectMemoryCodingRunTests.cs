using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Memory;

/// <summary>
/// 2026-11-10-fcd7c: a coding run and the project's own memory — remember '@project' needs no code change and
/// carries the run and spec, the CLI neither offers nor keeps it, and relied_on and dispute_memory name a project
/// entry as '@project/slug', recorded under the project's ledger key.
/// </summary>
public sealed class ProjectMemoryCodingRunTests
{
    private const string Root = "/work";
    private const string Spec = "2026-11-10-0001a";
    private readonly RecordingEventPublisher _events = new();
    private readonly InMemorySandboxFileReader _files = new();

    private static PipelineContext Run()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, "sample");
        pipeline.Set(ContextKeys.RunId, "run-7");
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft(Spec, "goal", "spec: x", []));
        return pipeline;
    }

    private IRunRepositories Repositories() =>
        new FixedRunRepositories(new Dictionary<string, ISandboxFileReader> { ["api"] = _files });

    private MasterMemoryUse Memory(PipelineContext pipeline, IProjectMemoryRepository rows) =>
        new(pipeline, Repositories(), Root,
            new MemoryCitationResolver(new MemoryRepositoryKey(), new MemorySources(rows, new MemoryRepositoryKey())),
            new MemoryUseRecorder(_events, "run-7", Spec, "sample", "A-1", NullLogger.Instance));

    private static ProjectMemoryDesignTurnTests.ProjectRows Rows(params string[] slugs)
    {
        var rows = new ProjectMemoryDesignTurnTests.ProjectRows();
        foreach (var slug in slugs)
            rows.Rows.Add(new ProjectMemoryRecord(slug, "feedback", "d", "b", ProjectMemorySourceKinds.Run, "run-1", null, null));
        return rows;
    }

    [Fact]
    public async Task CodingRun_RememberAtProject_WritesWithoutACodeChange_WithRunProvenance()
    {
        var rows = Rows();
        var host = new MemoryWriteToolHost(Repositories(), Root, null, new MemorySources(rows, new MemoryRepositoryKey()), Run());

        var answer = await host.Remember("feedback", "contracts first", "Build the contracts project first", "body", "@project");

        answer.Should().Contain("kept on the project");
        rows.Rows.Should().ContainSingle().Which.Should().Match<ProjectMemoryRecord>(r =>
            r.Slug == "contracts-first" && r.SourceKind == ProjectMemorySourceKinds.Run && r.SourceId == "run-7"
            && r.SourceRef == Spec);
        host.GetTools(null, null).Single().Description.Should().Contain("'@project'");
    }

    // 2026-11-10-4778 review: a scan or review master's remember has no '@project' target; only a coding master's does.
    [Fact]
    public async Task MasterRecordToolHosts_ScanOrReviewMaster_RememberCannotWriteAtProject()
    {
        var rows = Rows();
        var pipeline = Run();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["api"] = Moq.Mock.Of<ISandbox>() });
        var hosts = Handlers.MasterHandlerFixture.RecordHosts(new Handlers.MasterHandlerFixture.NoOpDecisionLogger(), projectMemory: rows);

        var scan = hosts.Create(pipeline, Root).Remember;
        var coding = hosts.Create(pipeline, Root, codingMaster: true).Remember;

        scan.OffersProject.Should().BeFalse();
        scan.GetTools(null, null).Single().Description.Should().NotContain("@project");
        (await scan.Remember("feedback", "x", "d", "b", "@project")).Should().Be(MemoryWriteToolHost.NoProjectStore);
        rows.Rows.Should().BeEmpty();
        coding.OffersProject.Should().BeTrue();
    }

    [Fact]
    public async Task CodingRun_RememberAtProject_AuthorNamesThePipelineAndTheTriggeringTicket()
    {
        var rows = Rows();
        var pipeline = Run();
        pipeline.Set(ContextKeys.PipelineName, "fix-bug");
        pipeline.Set(ContextKeys.TicketId, new AgentSmith.Domain.Models.TicketId("A-1"));
        var host = new MemoryWriteToolHost(Repositories(), Root, null, new MemorySources(rows, new MemoryRepositoryKey()), pipeline);

        await host.Remember("feedback", "contracts first", "Build the contracts project first", "body", "@project");

        rows.Rows.Single().Author.Should().Be("coding master (pipeline fix-bug, ticket A-1)");
    }

    [Fact]
    public async Task Recall_AtProjectEntry_QuotesTheBodyAndNamesWhereItCameFrom()
    {
        var rows = new ProjectMemoryDesignTurnTests.ProjectRows();
        rows.Rows.Add(new ProjectMemoryRecord("contracts-first", "feedback", "Contracts first", "Build contracts.\nThen the rest.",
            ProjectMemorySourceKinds.Conversation, "conv-1", "2", "Ada"));
        var recall = new MemoryRecallToolHost(Repositories(), Root, TestHelpers.MemoryStandings.Reader(), Run(), null,
            new MemorySources(rows, new MemoryRepositoryKey()));

        var text = await recall.Recall("contracts");

        text.Should().Contain("> Build contracts.\n> Then the rest.")
            .And.Contain("(kept on the project from conversation conv-1, turn 2, by \"Ada\")");
    }

    [Fact]
    public async Task CodingRun_NoProjectStore_DescriptionOmitsAtProject_CallIsRefused()
    {
        var host = new MemoryWriteToolHost(Repositories(), Root, null,
            new MemorySources(new NullProjectMemoryRepository(), new MemoryRepositoryKey()), Run());

        host.GetTools(null, null).Single().Description.Should().NotContain("@project");
        (await host.Remember("feedback", "x", "d", "b", "@project")).Should().Be(MemoryWriteToolHost.NoProjectStore);
    }

    [Fact]
    public async Task HandForward_ReliedOnAtProjectSlug_RecordsUnderTheProjectKey()
    {
        var pipeline = Run();
        var hand = new HandForwardToolHost(pipeline, Spec, Memory(pipeline, Rows("contracts-first")));

        var answer = await hand.HandForward(relied_on: ["@project/contracts-first"]);

        answer.Should().Contain("relied on 1 memories");
        _events.Events.OfType<MemoryUseRecordedEvent>().Single().Relied.Should().Equal(
            new MemoryCitation(ProjectMemorySource.Label, "project-memory:42", "contracts-first"));
        hand.GetTools(null, null).OfType<AIFunction>().Single().Description.Should().Contain("'@project/slug'");
    }

    [Fact]
    public async Task DisputeMemory_AtProjectSlug_ContradictsTheProjectEntry()
    {
        var host = new MemoryDisputeToolHost(Memory(Run(), Rows("contracts-first")));

        var answer = await host.DisputeMemory("contracts-first", "the contracts build last here", "@project");

        answer.Should().Contain("recorded");
        _events.Events.OfType<MemoryUseRecordedEvent>().Single().Disputed.Should()
            .Be(new MemoryCitation(ProjectMemorySource.Label, "project-memory:42", "contracts-first"));
    }
}
