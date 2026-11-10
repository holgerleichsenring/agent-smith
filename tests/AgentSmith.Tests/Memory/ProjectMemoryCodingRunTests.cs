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
