using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Memory.Standing;

/// <summary>
/// 2026-10-25-9324b: hand_forward's relied_on names stored memories — checked against their repository's store
/// and published as the spec's relied list — and dispute_memory publishes one contradiction with its reason.
/// </summary>
public sealed class MemoryRelianceTests
{
    private const string Root = "/work";
    private const string Spec = "2026-10-25-0001a";
    private readonly RecordingEventPublisher _events = new();
    private readonly Dictionary<string, InMemorySandboxFileReader> _repos = new(StringComparer.Ordinal);

    private InMemorySandboxFileReader Repo(string name, params string[] slugs)
    {
        var files = new InMemorySandboxFileReader();
        foreach (var slug in slugs)
            files.Files[$"{Root}/.agentsmith/memory/{slug}.md"] =
                $"---\nname: {slug}\ndescription: d\nmetadata:\n  type: project\n---\n\nbody\n";
        return _repos[name] = files;
    }

    private MasterMemoryUse Memory(PipelineContext? pipeline = null)
    {
        pipeline ??= new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
        {
            Repos = [new RepoConnection { Name = "api", Url = "https://github.com/o/api" }],
        });
        var repositories = new FixedRunRepositories(_repos.ToDictionary(kv => kv.Key, kv => (ISandboxFileReader)kv.Value));
        return new MasterMemoryUse(pipeline, repositories, Root, new MemoryCitationResolver(new()),
            new MemoryUseRecorder(_events, "run-1", Spec, "sample", "A-1", NullLogger.Instance));
    }

    [Fact]
    public async Task HandForward_ReliedOnKnownSlug_PublishesReliedList()
    {
        Repo("api", "build-order", "no-shims");
        var pipeline = new PipelineContext();

        var answer = await new HandForwardToolHost(pipeline, Spec, Memory(pipeline))
            .HandForward(persisted: ["table T"], relied_on: ["build-order", "api/no-shims", "[[build-order]]"]);

        answer.Should().Contain("relied on 2 memories");
        var used = _events.Events.OfType<MemoryUseRecordedEvent>().Should().ContainSingle().Subject;
        (used.Type, used.RunId, used.SpecId, used.Project, used.TicketId, used.Disputed)
            .Should().Be((EventType.MemoryUseRecorded, "run-1", Spec, "sample", "A-1", (MemoryCitation?)null));
        used.Relied.Should().Equal(new MemoryCitation("api", "github.com/o/api", "build-order"),
            new MemoryCitation("api", "github.com/o/api", "no-shims"));
        // 2026-10-25-9324c: kept on the pipeline for the run-end outcome.
        pipeline.Get<IReadOnlyDictionary<string, IReadOnlyList<MemoryCitation>>>(ContextKeys.MemoryReliance)[Spec]
            .Should().Equal(used.Relied);
    }

    [Fact]
    public async Task HandForward_ReliedOnUnknownSlug_ReturnsError()
    {
        Repo("api", "build-order");
        Repo("web", "build-order");
        var pipeline = new PipelineContext();

        var answer = await new HandForwardToolHost(pipeline, Spec, Memory(pipeline))
            .HandForward(persisted: ["table T"], relied_on: ["made-up", "build-order", "cli/build-order"]);

        answer.Should().StartWith("Error: hand_forward refused, nothing was recorded")
            .And.Contain("'made-up' is no stored memory")
            .And.Contain("'build-order' is stored in [api, web] — name it as 'repository/slug'")
            .And.Contain("'cli/build-order' names no repository of this run");
        _events.Events.Should().BeEmpty();
        pipeline.TryGet<object>(ContextKeys.SpecBuildNotes, out _).Should().BeFalse("a refused call records no note");
    }

    [Fact]
    public async Task DisputeMemory_PublishesOneContradictionWithItsReason()
    {
        Repo("api", "build-order");
        var host = new MemoryDisputeToolHost(Memory());

        var answer = await host.DisputeMemory("build-order", "the contracts project builds last here");

        answer.Should().Contain("Dispute of 'build-order' in api recorded");
        var used = _events.Events.OfType<MemoryUseRecordedEvent>().Should().ContainSingle().Subject;
        (used.Disputed, used.Reason).Should().Be((new MemoryCitation("api", "github.com/o/api", "build-order"),
            "the contracts project builds last here"));
        used.Relied.Should().BeEmpty();
        (await host.DisputeMemory("build-order", " ")).Should().StartWith("Error:");
        (await host.DisputeMemory("gone", "wrong")).Should().StartWith("Error: nothing was disputed");
    }

    [Fact]
    public async Task MemoryReliance_SurvivesTheCheckpoint()
    {
        Repo("api", "build-order");
        var pipeline = new PipelineContext();
        await new HandForwardToolHost(pipeline, Spec, Memory(pipeline)).HandForward(relied_on: ["build-order"]);
        var serializer = new AgentSmith.Application.Services.Resume.PipelineContextSerializer(
            NullLogger<AgentSmith.Application.Services.Resume.PipelineContextSerializer>.Instance);

        var restored = new PipelineContext();
        serializer.Restore(serializer.Serialize(pipeline), restored);

        restored.Get<IReadOnlyDictionary<string, IReadOnlyList<MemoryCitation>>>(ContextKeys.MemoryReliance)[Spec]
            .Should().Equal(new MemoryCitation("api", "github.com/o/api", "build-order"));
    }

    [Fact]
    public void MasterMemoryUse_WithoutRunId_CannotRecord() =>
        new MasterMemoryUse(new PipelineContext(), new FixedRunRepositories(new Dictionary<string, ISandboxFileReader>()),
            Root, new MemoryCitationResolver(new()), recorder: null).CanRecord.Should().BeFalse();
}
