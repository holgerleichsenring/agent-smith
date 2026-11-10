using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Memory;

/// <summary>
/// 2026-11-10-fcd7b: a design turn and the project's own memory — composed into the index first and quoted even
/// with no repository in scope, recall and dispute joining over it, remember only for a poster who may start runs
/// and kept with the conversation turn and its author, '@project/slug' citations, and the index section's remember
/// hint following the seeded flag.
/// </summary>
public sealed class ProjectMemoryDesignTurnTests
{
    private const string DesignMaster = "design-partner-master";

    internal sealed class ProjectRows(string name = "sample", long id = 42) : IProjectMemoryRepository
    {
        public List<ProjectMemoryRecord> Rows { get; } = [];
        public bool IsAvailable => true;
        public Task<long?> ResolveProjectAsync(string project, CancellationToken ct) =>
            Task.FromResult<long?>(project == name ? id : null);
        public Task<IReadOnlyList<ProjectMemoryRecord>> ListAsync(long projectId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProjectMemoryRecord>>([.. Rows]);
        public Task UpsertAsync(long projectId, ProjectMemoryRecord record, CancellationToken ct)
        {
            Rows.RemoveAll(r => r.Slug == record.Slug);
            Rows.Insert(0, record);
            return Task.CompletedTask;
        }
        public Task<bool> DeleteAsync(long projectId, string slug, CancellationToken ct) => Task.FromResult(false);
    }

    private static ProjectMemoryRecord Said(string slug) => new(
        slug, "feedback", "Never drop a feature the material records", "body", ProjectMemorySourceKinds.Conversation,
        "conv-1", "2", "Holger");

    private static ProjectMemoryIndex Index(IProjectMemoryRepository rows) =>
        new(rows, new MemoryRepositoryKey(), MemoryStandings.Reader(), new MemoryIndexComposer());

    private static PipelineContext Turn(bool mayStartRuns = false)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, "sample");
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        pipeline.Set(ContextKeys.DialogueJobId, "conv-9");
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
        [
            new SpecDialogTurn(SpecDialogTurn.UserRole, "we need widgets", Author: "Ana"),
            new SpecDialogTurn(SpecDialogTurn.AssistantRole, "Two questions.", SpecDialogTurnKind.Answer),
            new SpecDialogTurn(SpecDialogTurn.UserRole, "never drop a feature", Author: "Holger"),
        ]);
        if (mayStartRuns) pipeline.Set(ContextKeys.MayStartRuns, true);
        return pipeline;
    }

    [Fact]
    public async Task GroundSpecDialog_NoRepositoryInScope_StillComposesProjectMemory()
    {
        var rows = new ProjectRows();
        rows.Rows.Add(Said("never-drop-features"));
        var pipeline = Turn();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, []);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox>());
        var handler = new GroundSpecDialogHandler(null!, NullLogger<GroundSpecDialogHandler>.Instance,
            new DesignMemoryIndex(MemoryStandings.Reader(), new MemoryIndexComposer(), Index(rows)));

        await handler.ExecuteAsync(new GroundSpecDialogContext(pipeline), CancellationToken.None);

        pipeline.Get<string>(ContextKeys.MemoryIndex).Should().StartWith("> - [never-drop-features]")
            .And.Contain("conversation conv-1, turn 2, by Holger");
    }

    [Fact]
    public async Task MemoryIndex_OneRepositoryPlusProject_LabelledProjectFirstQuoted()
    {
        var rows = new ProjectRows();
        rows.Rows.Add(Said("never-drop-features"));
        var repo = new ContextDocument("api", null, null, "/work/.agentsmith/memory/MEMORY.md", "- [no-shims](no-shims.md) (feedback) — Migrate callers");

        var index = await new DesignMemoryIndex(MemoryStandings.Reader(), new MemoryIndexComposer(), Index(rows))
            .ComposeAsync(Turn(), [repo], default);

        index.IndexOf("## @project", StringComparison.Ordinal).Should().Be(0);
        index.Should().Contain("> - [never-drop-features]").And.Contain("## api").And.Contain("[no-shims]");
        index.IndexOf("## api", StringComparison.Ordinal).Should().BeGreaterThan(index.IndexOf("never-drop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DesignTurn_OnlyProjectMemory_RecallAndDisputeJoin()
    {
        var rows = new ProjectRows();
        rows.Rows.Add(Said("never-drop-features"));
        var loop = new ToolCapture();
        var context = DesignContext(mayStartRuns: false);
        context.Pipeline.Set(ContextKeys.MemoryIndex,
            await new DesignMemoryIndex(MemoryStandings.Reader(), new MemoryIndexComposer(), Index(rows))
                .ComposeAsync(context.Pipeline, [], default));

        await Run(loop, context, rows);

        loop.Names.Should().Contain("recall").And.Contain("dispute_memory").And.NotContain("remember");
    }

    [Fact]
    public async Task DesignTurn_PosterWithoutRunsControl_HasNoRemember()
    {
        var without = new ToolCapture();
        await Run(without, DesignContext(mayStartRuns: false), new ProjectRows());
        var with = new ToolCapture();
        await Run(with, DesignContext(mayStartRuns: true), new ProjectRows());

        without.Names.Should().NotContain("remember");
        with.Names.Should().Contain("remember");
    }

    [Fact]
    public async Task DesignTurn_Remember_WritesToTheProjectWithConversationIndexAndAuthor()
    {
        var rows = new ProjectRows();
        var host = new ProjectMemoryWriteToolHost(new MemorySources(rows, new MemoryRepositoryKey()), Turn(mayStartRuns: true));

        var answer = await host.Remember("feedback", "Never drop features", "Never drop a recorded feature", "the body");

        answer.Should().Contain("kept on the project");
        rows.Rows.Should().ContainSingle().Which.Should().Match<ProjectMemoryRecord>(r =>
            r.Slug == "never-drop-features" && r.SourceKind == ProjectMemorySourceKinds.Conversation
            && r.SourceId == "conv-9" && r.SourceRef == "2" && r.Author == "Holger");
    }

    [Fact]
    public async Task DesignTurn_Remember_OverTheCap_IsRefusedAndNothingStored()
    {
        var rows = new ProjectRows();
        var host = new ProjectMemoryWriteToolHost(new MemorySources(rows, new MemoryRepositoryKey()), Turn(mayStartRuns: true));

        var answer = await host.Remember("feedback", "x", new string('d', ProjectMemoryLimits.Description + 1), "b");

        answer.Should().StartWith("Not recorded").And.Contain("description");
        rows.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task MemoryCitation_AtProjectSlug_ResolvesToTheProjectKey()
    {
        var rows = new ProjectRows();
        rows.Rows.Add(Said("never-drop-features"));
        var resolver = new MemoryCitationResolver(new MemoryRepositoryKey(), new MemorySources(rows, new MemoryRepositoryKey()));

        var read = await resolver.ResolveAsync(Turn(), TestRepositories.One(new InMemorySandboxFileReader(), "api"), "/work",
            ["@project/never-drop-features"], default);

        read.Problems.Should().BeEmpty();
        read.Citations.Should().ContainSingle().Which.Should().Match<MemoryCitation>(c =>
            c.Repository == ProjectMemorySource.Label && c.RepoKey == "project-memory:42");
    }

    [Fact]
    public async Task MemoryCitation_BareSlugInProjectAndRepository_IsRefusedNamingBothForms()
    {
        var rows = new ProjectRows();
        rows.Rows.Add(Said("no-shims"));
        var files = new InMemorySandboxFileReader();
        await new FileMemoryStore(files, "/work").UpsertAsync(new MemoryEntry("no-shims", "d", MemoryEntryType.Feedback, "b"), default);
        var resolver = new MemoryCitationResolver(new MemoryRepositoryKey(), new MemorySources(rows, new MemoryRepositoryKey()));

        var read = await resolver.ResolveAsync(Turn(), TestRepositories.One(files, "api"), "/work", ["no-shims"], default);

        read.Citations.Should().BeEmpty();
        read.Problems.Should().ContainSingle().Which.Should().Contain("'@project/slug' or 'repository/slug'");
    }

    [Fact]
    public async Task MemoryRecall_ProjectEntry_IsReturnedFirst()
    {
        var rows = new ProjectRows();
        rows.Rows.Add(Said("drop-nothing"));
        var files = new InMemorySandboxFileReader();
        await new FileMemoryStore(files, "/work").UpsertAsync(new MemoryEntry("drop-old", "drop the old path", MemoryEntryType.Project, "b"), default);
        var recall = new MemoryRecallToolHost(TestRepositories.One(files, "api"), "/work", MemoryStandings.Reader(), Turn(),
            sources: new MemorySources(rows, new MemoryRepositoryKey()));

        var answer = await recall.Recall("drop");

        answer.IndexOf("drop-nothing", StringComparison.Ordinal).Should().BeLessThan(answer.IndexOf("drop-old", StringComparison.Ordinal));
        answer.Should().Contain("— the project");
    }

    [Fact]
    public void MemoryIndexSection_DesignTurnWithRunsControl_SaysUseRemember()
    {
        var pipeline = Turn(mayStartRuns: true);
        pipeline.Set(ContextKeys.MemoryIndex, "> - [x](x.md) (feedback) — y");

        MasterPromptSections.BuildMemoryIndexSection(pipeline).Should().Contain("use remember");
    }

    [Fact]
    public void MemoryIndexSection_DesignTurnWithoutRunsControl_NoRemember()
    {
        var pipeline = Turn(mayStartRuns: false);
        pipeline.Set(ContextKeys.MemoryIndex, "> - [x](x.md) (feedback) — y");

        MasterPromptSections.BuildMemoryIndexSection(pipeline).Should().NotContain("remember");
    }

    private static AgenticMasterContext DesignContext(bool mayStartRuns)
    {
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        context.Pipeline.Set(ContextKeys.RunId, "2026-11-10T2100-design");
        context.Pipeline.Set(ContextKeys.ProjectName, "sample");
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "never drop a feature", Author: "Holger")]);
        if (mayStartRuns) context.Pipeline.Set(ContextKeys.MayStartRuns, true);
        return context;
    }

    private static Task Run(ToolCapture loop, AgenticMasterContext context, IProjectMemoryRepository rows) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"),
                limits: new LoopLimitsConfig(), projectMemory: rows)
            .ExecuteAsync(context, CancellationToken.None);

    private sealed class ToolCapture : AgentSmith.Application.Services.Loop.IAgenticLoopRunner
    {
        public IReadOnlyList<string> Names { get; private set; } = [];

        public Task<AgentSmith.Application.Services.Loop.AgenticLoopResult> RunAsync(
            AgentSmith.Application.Services.Loop.AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            if (Names.Count == 0) Names = [.. request.Tools!.OfType<AIFunction>().Select(t => t.Name)];
            return Task.FromResult(new AgentSmith.Application.Services.Loop.AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "An answer with no outcome block."))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1)));
        }
    }
}
