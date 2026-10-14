using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Verify;

/// <summary>
/// Which declared stages a sandbox runs: a guarded stage over what its repository changed,
/// an unguarded one over what its own branch changed. Scripted sandboxes answer the
/// name-only read; the git-backed cases live in <see cref="StageRelevanceGitTests"/>.
/// </summary>
public sealed class StageRelevanceTests
{
    internal const string SourceDiff =
        "diff --git a/src/Api/Program.cs b/src/Api/Program.cs\n--- a/src/Api/Program.cs\n+++ b/src/Api/Program.cs\n@@ -1 +1 @@\n+x\n";

    private static readonly ContextYamlVerifyStage Guarded =
        new("dashboard-test", "pnpm --dir web test", WhenChanged: ["web/**", "contracts/*.cs"]);

    private static readonly ContextYamlVerifyStage Unguarded = new("build", "dotnet build");

    [Fact]
    public async Task StageRelevance_GuardedStage_NoMatchingPath_IsSkippedWithANote()
    {
        var (changes, delivered, notes) = await ReadAsync(Repo("shop"), ("shop-dotnet", Changed("src/Api/Program.cs")));

        Select("shop-dotnet", changes, delivered, notes, Guarded, Unguarded)!.Single().Stages
            .Should().Equal([Unguarded], "the guard matches nothing this branch changed");
        notes.Searched.Should().ContainSingle().Which.Should().Contain("dashboard-test skipped");
    }

    [Fact]
    public async Task StageRelevance_GuardedStageSkipped_NoteNamesTheGlobs()
    {
        var (changes, delivered, notes) = await ReadAsync(Repo("shop"), ("shop-dotnet", Changed("src/Api/Program.cs")));

        Select("shop-dotnet", changes, delivered, notes, Guarded);
        changes.Conclude(notes);

        notes.Searched.Single().Should().Contain("[web/**, contracts/*.cs]");
        notes.NoStageApplies.Should().ContainSingle().Which.Should()
            .Be("shop: no stage applies to this change — when_changed globs [web/**, contracts/*.cs] match nothing its branch changed");
    }

    [Fact]
    public async Task StageRelevance_GuardedStage_RepositoryUndetermined_Runs()
    {
        var (changes, delivered, notes) = await ReadAsync(Repo("shop"), ("shop-dotnet", NameList.Failed()),
            diffFailed: true);

        Select("shop-dotnet", changes, delivered, notes, Guarded)!.Single().Stages.Should().Equal([Guarded],
            "what an unreadable branch changed is unknown, and unknown is not unchanged");
    }

    [Fact]
    public async Task StageRelevance_UnguardedStage_SandboxUnchanged_IsSkippedAsToday()
    {
        var (changes, delivered, notes) = await ReadAsync(Repo("shop"),
            ("shop-dotnet", Changed()), ("shop-node", Changed("web/app.ts")));

        Select("shop-dotnet", changes, delivered, notes, Unguarded).Should().BeNull(
            "a stage without when_changed keeps the per-sandbox rule");
        notes.Searched.Should().BeEmpty();
    }

    [Fact]
    public async Task StageRelevance_EveryStageSkipped_SkipsTheSandbox_NeverInfers()
    {
        var (changes, delivered, notes) = await ReadAsync(Repo("shop"), ("shop-dotnet", Changed("src/Api/Program.cs")));

        Select("shop-dotnet", changes, delivered, notes, Guarded).Should().BeNull(
            "an empty declared list would hand the sandbox to the analyzer's guess");
    }

    [Fact]
    public async Task StageRelevance_OtherRepositoryChanged_DoesNotTrigger()
    {
        var pipeline = Repo("shop", "web");
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["shop"] = "shop", ["web"] = "web" });
        var (changes, delivered, notes) = await ReadAsync(pipeline, ("shop", Changed()), ("web", Changed("web/app.ts")));

        Select("shop", changes, delivered, notes, Guarded).Should().BeNull("web/app.ts changed in another repository");
        Select("web", changes, delivered, notes, Guarded).Should().NotBeNull();
    }

    [Fact]
    public async Task StageRelevance_NameOnlyReadFails_RepositoryUndetermined_Runs()
    {
        var (changes, delivered, notes) = await ReadAsync(Repo("shop"),
            ("shop-dotnet", Changed()), ("shop-node", NameList.Failed()));

        changes.IsUndetermined("shop-dotnet").Should().BeTrue();
        Select("shop-dotnet", changes, delivered, notes, Guarded).Should().NotBeNull();
    }

    [Fact]
    public async Task StageRelevance_NameOnlyListAtTheStdoutCap_IsUndetermined_Runs()
    {
        var capped = new NameList(new string('a', SizeLimits.RunStepCapturedStdoutMaxChars), 0);
        var (changes, delivered, notes) = await ReadAsync(Repo("shop"), ("shop-dotnet", capped));

        changes.IsUndetermined("shop-dotnet").Should().BeTrue("a list at the capture cap may have been cut");
        Select("shop-dotnet", changes, delivered, notes, Guarded).Should().NotBeNull();
    }

    [Fact]
    public async Task StageRelevance_NoRepositoryMap_EachSandboxIsItsOwnRepository()
    {
        var (changes, delivered, notes) = await ReadAsync(new PipelineContext(),
            ("server", Changed()), ("client", Changed("web/app.ts")));

        changes.RepositoryOf("server").Should().Be("server");
        Select("server", changes, delivered, notes, Guarded).Should().BeNull(
            "with no map nothing joins two sandboxes into one repository");
        Select("client", changes, delivered, notes, Guarded).Should().NotBeNull();
    }

    internal static IReadOnlyList<ContextVerifyStages>? Select(
        string key, RepositoryChanges changes, DeliveredWork delivered, VerifyResolutionNotes notes,
        params ContextYamlVerifyStage[] stages) =>
        TestTreeSync.Relevance().Select(key, [new ContextVerifyStages("default", stages)], changes, delivered, notes);

    private static PipelineContext Repo(params string[] names)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [.. names.Select(n => new RepoConnection { Name = n })]);
        return pipeline;
    }

    private static NameList Changed(params string[] paths) =>
        new(paths.Length == 0 ? null : string.Join('\0', paths) + "\0\n", 0);

    private static async Task<(RepositoryChanges, DeliveredWork, VerifyResolutionNotes)> ReadAsync(
        PipelineContext pipeline, (string Key, NameList List) first, (string Key, NameList List)? second = null,
        bool diffFailed = false)
    {
        var entries = second is { } s ? new[] { first, s } : [first];
        var sandboxes = entries.ToDictionary(e => e.Key, e => (ISandbox)e.List, StringComparer.Ordinal);
        var diffs = entries.ToDictionary(e => e.Key, e => diffFailed
            ? new DeliveryDiff.DiffResult(string.Empty, "no comparable base", Failed: true)
            : new DeliveryDiff.DiffResult(e.List.Output is null ? string.Empty : SourceDiff, "against origin/main", BaseRef: "origin/main"),
            StringComparer.Ordinal);
        var changes = await TestTreeSync.Relevance().ReadAsync(pipeline, sandboxes, diffs, CancellationToken.None);
        return (changes, DeliveredWork.Of(diffs), new VerifyResolutionNotes());
    }

    /// <summary>Answers the unquoted -z name-only read with a fixed list and exit code.</summary>
    private sealed class NameList(string? output, int exitCode) : ISandbox
    {
        public string? Output => output;

        public string JobId => "names";

        public static NameList Failed() => new(null, 128);

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            step.Args.Should().Equal("-c", "core.quotePath=false", "diff", "--name-only", "--no-renames", "-z", "origin/main");
            return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, exitCode,
                TimedOut: false, DurationSeconds: 0.01, ErrorMessage: null, OutputContent: output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
