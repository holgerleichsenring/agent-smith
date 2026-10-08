using System.Text;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-10-01-283df: real git. The uploaded websites a run carries sit inside the working tree, and
/// neither the commit's <c>git add -A</c> nor the run record's force-stage of the whole .agentsmith
/// directory — which ignores .git/info/exclude — puts a single one of their files in the commit.
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class ReferenceCommitExclusionTests
{
    [Fact]
    public async Task CommitAndPR_AfterMaterialize_StagesNoReferenceFile()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        var dir = Path.Combine(Path.GetTempPath(), "as-283df-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        await using var sandbox = new InProcessSandbox("test", dir, ownsWorkDir: true, NullLogger.Instance);
        await GitAsync(sandbox, "init", "-q", "-b", "main");
        var files = new SandboxFileReaderFactory();
        await new ReferenceGitExclusion(files).EnsureAsync(sandbox, CancellationToken.None);
        await new ReferenceSetMaterialiser(new NoReferenceSetReader(), files, new SandboxBinaryFileWriter()).WriteUnderAsync(sandbox,
            [new ReferenceSetFile("site/css/site.css", Encoding.UTF8.GetBytes("h1 { color: #c0ffee; }"))],
            ReferenceDirectory.ForSet("set-a"), CancellationToken.None);
        await files.Create(sandbox).WriteAsync(".agentsmith/runs/r-1/result.md", "# result", CancellationToken.None);
        await files.Create(sandbox).WriteAsync("src/app.css", "h1 { color: #c0ffee; }", CancellationToken.None);
        var git = new SandboxGitOperations(new GitBranchPusher(), AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver, NullLogger<SandboxGitOperations>.Instance,
            files, new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));

        await git.StageAllAsync(sandbox, CancellationToken.None);
        await git.ForceStageAsync(sandbox, ".agentsmith", CancellationToken.None);
        var staged = await GitAsync(sandbox, "diff", "--cached", "--name-only");

        staged.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().BeEquivalentTo(
            ["src/app.css", ".agentsmith/runs/r-1/result.md"],
            "the run record is force-staged and the source change is staged; no reference file is either");
    }

    private static async Task<string> GitAsync(ISandbox sandbox, params string[] args)
    {
        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: args, TimeoutSeconds: 30), null, CancellationToken.None);
        result.ExitCode.Should().Be(0, result.ErrorMessage);
        return result.OutputContent ?? string.Empty;
    }
}
