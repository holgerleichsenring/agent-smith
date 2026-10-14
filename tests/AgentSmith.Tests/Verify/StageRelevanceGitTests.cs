using System.Text;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Verify;

/// <summary>
/// The changed paths a guarded stage is matched against come from git itself, NUL-separated
/// and unquoted, against the ref the delivery diff was taken against. Real git, in-process
/// clones of one bare origin serving one repository.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class StageRelevanceGitTests
{
    private const string BaseRef = "origin/" + SandboxClones.Branch;

    private static readonly ContextYamlVerifyStage WebTest =
        new("web-test", "cd web && npm test", WhenChanged: ["web/**"]);

    [Fact]
    public async Task StageRelevance_GuardedStage_OtherSandboxChangedAMatchingPath_RunsInAnUnchangedSandbox()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 2;\n");

        (await SelectAsync(clones, clones.Primary)).Should().NotBeNull(
            "the stage guards what its command reads, whichever sandbox delivered it");
    }

    [Fact]
    public async Task StageRelevance_DeletedFileUnderGlob_Runs()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        File.Delete(clones.PathOf(clones.Primary, "web/app.ts"));

        (await SelectAsync(clones, clones.Primary)).Should().NotBeNull();
    }

    [Fact]
    public async Task StageRelevance_PureRenameIntoGlob_Runs()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.GitAsync(clones.Primary, "mv", "docs/crlf.txt", "web/crlf.txt");
        await clones.CommitAllAsync(clones.Primary, "rename");

        (await SelectAsync(clones, clones.Primary)).Should().NotBeNull("a rename counts under its new name");
    }

    [Fact]
    public async Task StageRelevance_BinaryFileUnderGlob_Runs()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteBytesAsync(clones.Primary, "web/logo.png", SandboxClones.Binary);
        await clones.CommitAllAsync(clones.Primary, "logo");

        (await SelectAsync(clones, clones.Primary)).Should().NotBeNull();
    }

    [Fact]
    public async Task StageRelevance_NonAsciiPathUnderGlob_Runs()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "web/café.ts", "export const cafe = 1;\n");
        await clones.CommitAllAsync(clones.Primary, "cafe");

        (await SelectAsync(clones, clones.Primary)).Should().NotBeNull(
            "a quoted path would start with '\"web' and match no glob");
    }

    [Fact]
    public async Task StageRelevance_DashboardPathBeyondTheStdoutCap_StillRuns()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        var big = new StringBuilder();
        while (big.Length <= SizeLimits.RunStepCapturedStdoutMaxChars + 100_000)
            big.Append("generated line of a designer file, long enough to pass the cap\n");
        await clones.WriteAsync(clones.Primary, "src/Big.Designer.cs", big.ToString());
        await clones.WriteAsync(clones.Primary, "web/app.ts", "export const app = 3;\n");
        await clones.CommitAllAsync(clones.Primary, "big");

        var text = await clones.GitAsync(clones.Primary, "diff", "--no-color", BaseRef);
        text.Should().NotContain("web/app.ts", "the premise: the captured diff text stops before the dashboard path");
        (await SelectAsync(clones, clones.Primary)).Should().NotBeNull();
    }

    private static async Task<IReadOnlyList<ContextVerifyStages>?> SelectAsync(SandboxClones clones, string key)
    {
        var diffs = new Dictionary<string, DeliveryDiff.DiffResult>(StringComparer.Ordinal);
        foreach (var sandbox in clones.Sandboxes.Keys)
            diffs[sandbox] = new DeliveryDiff.DiffResult(
                await clones.GitAsync(sandbox, "diff", "--no-color", BaseRef), $"against {BaseRef}", BaseRef: BaseRef);
        var changes = await TestTreeSync.Relevance().ReadAsync(clones.Pipeline, clones.Sandboxes, diffs, CancellationToken.None);
        changes.IsUndetermined(key).Should().BeFalse();
        return StageRelevanceTests.Select(key, changes, DeliveredWork.Of(diffs), new VerifyResolutionNotes(), WebTest);
    }
}
