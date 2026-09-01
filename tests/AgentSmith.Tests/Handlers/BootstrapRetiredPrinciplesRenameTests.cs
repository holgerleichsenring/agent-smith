using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-01-72c5: a repository initialised before the principles file was renamed carries
/// only the retired name, and the preserve path reads the current one — so the round reported
/// nothing existing and composed a fresh file over the operator's ratified section. The round
/// renames first, and everything downstream then sees a repository already on the new name.
/// </summary>
public sealed class BootstrapRetiredPrinciplesRenameTests
{
    private const string MetaDir = ".agentsmith/contexts/server";
    private const string CurrentPath = $"{MetaDir}/{ProjectMetaPaths.PrinciplesFile}";
    private const string RetiredPath = $"{MetaDir}/{ProjectMetaPaths.RetiredPrinciplesFile}";
    private const string ContextPath = $"{MetaDir}/{ProjectMetaPaths.ContextYamlFile}";

    private const string Ratified =
        "# Principles\ncore\n## Project Specifics (ratified additions)\n"
        + "Field changes go through the estate's own CLI.\n";

    private const string Composed = "# Principles\nFRESHLY-COMPOSED core + delta\n";

    private static readonly RoleSkillDefinition BootstrapSkill = new()
    {
        Name = "project-bootstrap",
        DisplayName = "Project Bootstrap",
        Description = "test",
        Emoji = "🔧",
        Rules = "test",
        Role = "producer",
        OutputSchema = "bootstrap",
    };

    [Fact]
    public async Task BootstrapRound_ARepositoryCarryingOnlyTheRetiredName_MovesItAndPreservesTheContent()
    {
        var repo = new MetaRepository((ContextPath, "meta:\n  workdir: server\n"), (RetiredPath, Ratified));

        var result = await RunRoundAsync(repo);

        // The assertion that matters: the file under the current name IS the operator's
        // ratified content, not a composition of core + delta written over it.
        repo.Files.Should().ContainKey(CurrentPath);
        repo.Files[CurrentPath].Should().Be(Ratified);
        repo.Files[CurrentPath].Should().NotContain("FRESHLY-COMPOSED");
        repo.Files.Should().NotContainKey(RetiredPath,
            "a move leaves no second file for the coding agent to find beside the first");
        repo.RanSteps.Should().NotContain(
            s => s.Kind == StepKind.WriteFile && s.Path == CurrentPath,
            "the preserve branch writes nothing — the ratified file is already there");
        MoveScripts(repo).Should().ContainSingle()
            .Which.Should().Contain("git mv").And.Contain("|| mv",
                "the rename is legible in the init pull request, and an untracked file "
                + "still moves rather than failing the round");
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("preserved").And.Contain("renamed");
    }

    [Fact]
    public async Task BootstrapRound_BothNamesPresent_LeavesTheCurrentFileUntouched()
    {
        var repo = new MetaRepository(
            (ContextPath, "meta:\n  workdir: server\n"),
            (CurrentPath, Ratified),
            (RetiredPath, "# superseded\n"));

        var result = await RunRoundAsync(repo);

        MoveScripts(repo).Should().BeEmpty(
            "whatever produced the current name is more recent than what stopped being written");
        repo.Files[CurrentPath].Should().Be(Ratified);
        repo.Files[RetiredPath].Should().Be("# superseded\n");
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().NotContain("renamed");
    }

    [Fact]
    public async Task BootstrapRound_NoRetiredFile_PerformsNoMove()
    {
        var repo = new MetaRepository((ContextPath, "meta:\n  workdir: server\n"));

        var result = await RunRoundAsync(repo);

        MoveScripts(repo).Should().BeEmpty("a cold init is unchanged");
        repo.Files[CurrentPath].Should().Be(Composed, "with nothing existing, the round composes");
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().NotContain("renamed");
    }

    [Fact]
    public async Task BootstrapRound_TheMoveFails_FailsInsteadOfComposingOverTheRatifiedFile()
    {
        var repo = new MetaRepository((ContextPath, "meta:\n  workdir: server\n"), (RetiredPath, Ratified))
        {
            MoveExitCode = 1,
        };

        var result = await RunRoundAsync(repo);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(ProjectMetaPaths.RetiredPrinciplesFile).And.Contain("refused");
        repo.Files.Should().NotContainKey(CurrentPath,
            "content that could not be migrated is never composed over");
        repo.Files[RetiredPath].Should().Be(Ratified);
    }

    private static IReadOnlyList<string> MoveScripts(MetaRepository repo) =>
        [.. repo.RanSteps
            .Where(s => s.Kind == StepKind.Run && s.Args is { Count: 2 })
            .Select(s => s.Args![1])
            .Where(script => script.Contains("mv ", StringComparison.Ordinal))];

    private static Task<CommandResult> RunRoundAsync(MetaRepository repo) =>
        NewHandler(repo).ExecuteAsync(
            new BootstrapRoundContext(
                BootstrapSkill.Name, "monorepo", new AgentConfig(), NewPipeline(repo),
                ContextName: "server", Workdir: "server"),
            CancellationToken.None);

    private static BootstrapRoundHandler NewHandler(MetaRepository repo) => new(
        new StubFactory(new WritingChatClient()),
        new BootstrapToolHostFactory(
            Mock.Of<IDecisionLogger>(),
            new PathReadGuard(new NullGitIgnoreResolver()),
            new PathWriteGuard(new PathReadGuard(new NullGitIgnoreResolver())),
            ContextGates.Serializer(),
            ContextGates.Build(), ContextGates.Writer(), ContextGates.DerivationStamp()),
        BootstrapReaderStubs.MetaFilesOver(repo),
        PrinciplesTransferStubs.Composing(Composed),
        new BootstrapContextWriteVerdict(),
        new BootstrapOutputRecorder(),
        EventTestStubs.RunContext,
        NullLogger<BootstrapRoundHandler>.Instance);

    private static PipelineContext NewPipeline(ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
            "init-project", new AgentConfig(), "skills", null));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["monorepo"] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { ["monorepo"] = NewMap() });
        pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, new[] { BootstrapSkill });
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://x/y.git"));
        return pipeline;
    }

    private static ProjectMap NewMap() =>
        new(PrimaryLanguage: "csharp",
            Frameworks: [],
            Modules: [],
            TestProjects: [],
            EntryPoints: [],
            Conventions: new Conventions(NamingPattern: null, TestLayout: null, ErrorHandling: null),
            Ci: new CiConfig(HasCi: false, BuildCommand: null, TestCommand: null, CiSystem: null));

    /// <summary>
    /// One repository's meta directory, read and written through the same file map — so a
    /// move performed by a step is visible to the read that follows it. Absolute sandbox
    /// paths and repo-relative ones name the same file, as they do in /work.
    /// </summary>
    private sealed class MetaRepository(params (string Path, string Content)[] files)
        : ISandbox, ISandboxFileReaderFactory, ISandboxFileReader
    {
        public Dictionary<string, string> Files { get; } =
            files.ToDictionary(f => f.Path, f => f.Content, StringComparer.Ordinal);

        public List<Step> RanSteps { get; } = [];

        /// <summary>A sandbox that refuses the move, e.g. a read-only meta directory.</summary>
        public int MoveExitCode { get; init; }

        public string JobId => "meta-repository";

        public ISandboxFileReader Create(ISandbox sandbox) => this;

        public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
            Task.FromResult(Files.ContainsKey(Rel(path)));

        public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
            Task.FromResult(Files.GetValueOrDefault(Rel(path)));

        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
            Task.FromResult(Files[Rel(path)]);

        public Task WriteAsync(string path, string content, CancellationToken ct)
        {
            Files[Rel(path)] = content;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([.. Files.Keys]);

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            RanSteps.Add(step);
            if (step.Kind == StepKind.WriteFile && step.Path is { } path)
                Files[Rel(path)] = step.Content ?? string.Empty;
            var exitCode = step.Kind == StepKind.Run ? ApplyMove(step) : 0;
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode,
                TimedOut: false, DurationSeconds: 0.01,
                ErrorMessage: exitCode == 0 ? null : "refused", OutputContent: null));
        }

        // `mv` semantics for the one script the round issues: the retired file the script
        // names ends up under the current name, and nothing is left at the old one.
        private int ApplyMove(Step step)
        {
            if (MoveExitCode != 0) return MoveExitCode;
            var script = step.Args is { Count: 2 } args ? args[1] : string.Empty;
            var retired = Files.Keys.FirstOrDefault(
                key => key.EndsWith(ProjectMetaPaths.RetiredPrinciplesFile, StringComparison.Ordinal)
                       && script.Contains(key, StringComparison.Ordinal));
            if (retired is null) return 0;
            Files[retired.Replace(
                ProjectMetaPaths.RetiredPrinciplesFile, ProjectMetaPaths.PrinciplesFile,
                StringComparison.Ordinal)] = Files[retired];
            Files.Remove(retired);
            return 0;
        }

        private static string Rel(string path) =>
            path.StartsWith("/work/", StringComparison.Ordinal) ? path["/work/".Length..] : path;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class WritingChatClient : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await BootstrapToolCall.WriteContextYamlAsync(options, BootstrapToolCall.ValidDocument);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class StubFactory(IChatClient client) : IChatClientFactory
    {
        public IChatClient Create(
            AgentConfig agent, TaskType task, int? maxIterations = null,
            MasterLoopHooks? masterLoopHooks = null) => client;

        public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;

        public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
    }
}
