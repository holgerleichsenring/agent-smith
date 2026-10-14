using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.Integration;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// Every sandbox of one repository holds the union of their work before a verify stage runs:
/// folded into the primary, committed through the secret scan, spread back and proven by the
/// tree hash. Real git, two or three in-process clones of one bare origin.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SandboxTreeSyncTests
{
    [Fact]
    public async Task SandboxTreeSync_SingleSandboxRepository_RunsNoGitStep()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        var steps = new List<Step>();
        var sandbox = new FaultingSandbox(new NoSandbox(), fault: step =>
        {
            steps.Add(step);
            return null;
        });
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["shop"] = sandbox });
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "shop" }]);

        var result = await TestTreeSync.Create().SyncAsync(pipeline, CancellationToken.None);

        result.IsLevel.Should().BeTrue();
        steps.Should().BeEmpty("a repository with one sandbox has nothing to level");
    }

    [Fact]
    public async Task SandboxTreeSync_PrimaryChangedCSharp_SecondarySeesItBeforeItsStages()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int x; }\n");

        var result = await SyncAsync(clones);

        result.IsLevel.Should().BeTrue(result.Message);
        (await clones.ReadAsync(clones.Secondary, "src/Api/Program.cs")).Should().Be("class Program { int x; }\n");
        (await clones.StatusAsync(clones.Secondary)).Should().BeEmpty("the secondary committed the union locally");
        (await clones.HeadTreeAsync(clones.Secondary)).Should().Be(await clones.HeadTreeAsync(clones.Primary));
    }

    [Fact]
    public async Task SandboxTreeSync_SecondaryChangedTypeScript_PrimaryCarriesItAndItIsCommitted()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 2;\n");

        var result = await SyncAsync(clones);

        result.IsLevel.Should().BeTrue(result.Message);
        (await clones.GitAsync(clones.Primary, "show", "HEAD:web/app.ts")).Should().Be("export const app = 2;\n");
        (await clones.StatusAsync(clones.Primary)).Should().BeEmpty();
        (await clones.GitAsync(clones.Primary, "ls-remote", "origin", SandboxClones.Branch))
            .Should().StartWith(await clones.HeadAsync(clones.Primary), "the union reached the branch");
    }

    [Fact]
    public async Task SandboxTreeSync_BothChangedOneFile_IsRedNamingFileAndBothKeys_NothingApplied()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int a; }\n");
        await clones.WriteAsync(clones.Secondary, "src/Api/Program.cs", "class Program { int b; }\n");
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 3;\n");
        var head = await clones.HeadAsync(clones.Primary);

        var result = await SyncAsync(clones);

        result.IsLevel.Should().BeFalse();
        result.Message.Should().Be(
            $"Sandbox tree sync: src/Api/Program.cs was changed in both {clones.Primary} and {clones.Secondary}");
        (await clones.ReadAsync(clones.Primary, "web/app.ts")).Should().Be("export const app = 1;\n");
        (await clones.ReadAsync(clones.Secondary, "src/Api/Program.cs")).Should().Be("class Program { int b; }\n");
        (await clones.HeadAsync(clones.Primary)).Should().Be(head, "nothing was committed");
    }

    [Fact]
    public async Task SandboxTreeSync_SecondSync_CarriesOnlyWorkSinceTheFirst()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int x; }\n");
        (await SyncAsync(clones)).IsLevel.Should().BeTrue();
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 4;\n");
        await clones.WriteAsync(clones.Primary, "src/Api/Extra.cs", "class Extra {}\n");

        var result = await SyncAsync(clones);

        result.IsLevel.Should().BeTrue(result.Message);
        (await clones.ReadAsync(clones.Primary, "web/app.ts")).Should().Be("export const app = 4;\n");
        (await clones.ReadAsync(clones.Secondary, "src/Api/Extra.cs")).Should().Be("class Extra {}\n");
        (await clones.GitAsync(clones.Secondary, "diff", "--name-only", "HEAD~1", "HEAD"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().BeEquivalentTo(["src/Api/Extra.cs", "web/app.ts"]);
    }

    [Fact]
    public async Task SandboxTreeSync_BinaryFileChange_ReachesEverySandbox()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteBytesAsync(clones.Secondary, "assets/logo.png", SandboxClones.Binary);

        var result = await SyncAsync(clones);

        result.IsLevel.Should().BeTrue(result.Message);
        (await clones.ReadBytesAsync(clones.Primary, "assets/logo.png")).Should().Equal(SandboxClones.Binary);
    }

    [Fact]
    public async Task SandboxTreeSync_CrlfFileChange_ReachesEverySandboxByteForByte()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "docs/crlf.txt", "one\r\ntwo\r\nthree\r\n");

        var result = await SyncAsync(clones);

        result.IsLevel.Should().BeTrue(result.Message);
        (await clones.ReadBytesAsync(clones.Secondary, "docs/crlf.txt"))
            .Should().Equal(System.Text.Encoding.UTF8.GetBytes("one\r\ntwo\r\nthree\r\n"));
    }

    [Fact]
    public async Task SandboxTreeSync_SandboxesWithIndependentBaseMergeCommits_SameTree_IsLevel()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        foreach (var key in new[] { clones.Primary, clones.Secondary })
        {
            await clones.WriteAsync(key, "base.txt", "the moved base\n");
            await clones.CommitAllAsync(key, $"merge the base into {key}");
        }
        var heads = (await clones.HeadAsync(clones.Primary), await clones.HeadAsync(clones.Secondary));
        heads.Item1.Should().NotBe(heads.Item2);

        var result = await SyncAsync(clones);

        result.IsLevel.Should().BeTrue(result.Message);
        (await clones.HeadAsync(clones.Primary), await clones.HeadAsync(clones.Secondary))
            .Should().Be(heads, "one tree is level, whatever commits carry it");
    }

    [Fact]
    public async Task SandboxTreeSync_SandboxesSharingOneWorkingDirectory_IsLevel()
    {
        await using var clones = await SandboxClones.CreateAsync(sharedDirectory: true);
        if (clones is null) return;
        var head = await clones.HeadAsync(clones.Primary);
        await clones.WriteAsync(clones.Primary, "web/app.ts", "export const app = 5;\n");

        var result = await SyncAsync(clones);

        result.IsLevel.Should().BeTrue(result.Message);
        (await clones.StatusAsync(clones.Primary)).Should().BeEmpty();
        (await clones.GitAsync(clones.Primary, "rev-parse", "HEAD~1")).Trim().Should().Be(head, "one commit, through the push");
    }

    [Fact]
    public async Task SandboxTreeSync_SharedWorkingDirectory_SecretScanDeclined_IsRedAndCommitsNothing()
    {
        await using var clones = await SandboxClones.CreateAsync(sharedDirectory: true);
        if (clones is null) return;
        var head = await clones.HeadAsync(clones.Primary);
        await clones.WriteAsync(clones.Primary, "web/app.ts", "export const key = 'leak';\n");

        var result = await SyncAsync(clones, new DecliningScanner());

        result.Message.Should().Contain("the union could not be committed (secret scan or push declined)");
        (await clones.HeadAsync(clones.Secondary)).Should().Be(head, "the sync committed nothing the scan declined");
    }

    [Fact]
    public async Task SandboxTreeSync_FoldedButNotSpread_NextSyncIsGreen()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 6;\n");
        await clones.WriteAsync(clones.Primary, "web/app.ts", "export const app = 6;\n");
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int y; }\n");
        await clones.CommitAllAsync(clones.Primary, "the fold, committed before the spread stopped");

        var result = await SyncAsync(clones);

        result.IsLevel.Should().BeTrue(result.Message);
        (await clones.HeadTreeAsync(clones.Secondary)).Should().Be(await clones.HeadTreeAsync(clones.Primary));
    }

    [Fact]
    public async Task SandboxTreeSync_GitDiffFails_IsRedNamingTheSandbox()
    {
        await using var clones = await SandboxClones.CreateAsync(wrap: (key, inner) => key != SandboxClones.Keys[1]
            ? inner
            : new FaultingSandbox(inner, step => step.Command == "sh" ? FaultingSandbox.Failed(step, "diff refused") : null));
        if (clones is null) return;
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 7;\n");

        var result = await SyncAsync(clones);

        result.Message.Should().StartWith($"Sandbox tree sync: {clones.Secondary}: git diff --binary failed");
        (await clones.ReadAsync(clones.Primary, "web/app.ts")).Should().Be("export const app = 1;\n");
    }

    [Fact]
    public async Task SandboxTreeSync_EncodedDeltaAtTheStdoutLimit_IsRedBeforeAnythingIsApplied()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        var noise = new byte[900_000];
        new Random(8762).NextBytes(noise);
        await clones.WriteBytesAsync(clones.Secondary, "assets/noise.bin", noise);
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 8;\n");

        var result = await SyncAsync(clones);

        result.Message.Should().Contain(clones.Secondary).And.Contain("1000000-character limit");
        File.Exists(clones.PathOf(clones.Primary, "assets/noise.bin")).Should().BeFalse();
        (await clones.ReadAsync(clones.Primary, "web/app.ts")).Should().Be("export const app = 1;\n");
    }

    [Fact]
    public async Task SandboxTreeSync_BaseTreeUnknownToThePrimary_IsRed()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Secondary, "local.txt", "committed only here\n");
        await clones.CommitAllAsync(clones.Secondary, "a commit the primary never saw");

        var result = await SyncAsync(clones);

        result.Message.Should().Contain(clones.Secondary).And.Contain("is unknown to " + clones.Primary);
    }

    [Fact]
    public async Task SandboxTreeSync_ApplyCheckFailsForOneSecondary_PrimaryIsUntouched()
    {
        var checks = 0;
        await using var clones = await SandboxClones.CreateAsync(count: 3, wrap: (key, inner) => key != SandboxClones.Keys[0]
            ? inner
            : new FaultingSandbox(inner, step => step.Args is { } args && args.Contains("--check") && ++checks == 2
                ? FaultingSandbox.Failed(step, "patch does not apply")
                : null));
        if (clones is null) return;
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 9;\n");
        await clones.WriteAsync(SandboxClones.Keys[2], "tools/run.py", "print(1)\n");

        var result = await SyncAsync(clones);

        result.Message.Should().Contain(SandboxClones.Keys[2]).And.Contain("does not apply");
        (await clones.ReadAsync(clones.Primary, "web/app.ts")).Should().Be("export const app = 1;\n",
            "every change is checked before any is applied");
        (await clones.StatusAsync(clones.Primary)).Should().BeEmpty();
    }

    [Fact]
    public async Task SandboxTreeSync_PrimaryStillDirtyAfterThePush_IsRed()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        var head = await clones.HeadAsync(clones.Secondary);
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 10;\n");

        var result = await SyncAsync(clones, new DecliningScanner());

        result.Message.Should().Be(
            $"Sandbox tree sync: {SandboxClones.Repo}: the union could not be committed (secret scan or push declined) in {clones.Primary}");
        (await clones.HeadAsync(clones.Secondary)).Should().Be(head, "nothing is spread before the union is committed");
    }

    [Fact]
    public async Task SandboxTreeSync_SpreadProducesAnotherTree_IsRedNamingTheSandbox()
    {
        string? secondaryDir = null;
        await using var clones = await SandboxClones.CreateAsync(wrap: (key, inner) => key != SandboxClones.Keys[1]
            ? inner
            : new FaultingSandbox(inner, after: (step, result) =>
            {
                if (step.Args is { } args && args.Contains("apply") && !args.Contains("--check") && result.ExitCode == 0)
                    File.WriteAllText(Path.Combine(secondaryDir!, "stray.txt"), "not in the union\n");
            }));
        if (clones is null) return;
        secondaryDir = Path.GetDirectoryName(clones.PathOf(clones.Secondary, "x"));
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int z; }\n");

        var result = await SyncAsync(clones);

        result.Message.Should().StartWith($"Sandbox tree sync: {clones.Secondary}: after the spread it holds tree")
            .And.Contain("not the union tree");
    }

    [Fact]
    public async Task SandboxTreeSync_AfterSync_PrConsolidationFindsNothingToFold()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 11;\n");
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int w; }\n");
        (await SyncAsync(clones)).IsLevel.Should().BeTrue();

        var folded = await TestTreeSync.GitOps().ConsolidateSecondarySandboxesAsync(
            clones.Matches, clones.Sandboxes[clones.Primary], CancellationToken.None);

        folded.Should().Be(0, "every secondary's work is already on the primary's branch");
    }

    private static Task<SandboxTreeSyncResult> SyncAsync(SandboxClones clones, ISecretPatternScanner? scanner = null) =>
        TestTreeSync.Create(new SandboxTargets(), scanner).SyncAsync(clones.Pipeline, CancellationToken.None);

    private sealed class DecliningScanner : ISecretPatternScanner
    {
        public IReadOnlyList<SecretMatch> Scan(string path, string content) => [new SecretMatch(path, 1, "test-pattern")];
    }

    private sealed class NoSandbox : ISandbox
    {
        public string JobId => "none";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct) =>
            Task.FromResult(FaultingSandbox.Failed(step, "no step expected"));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
