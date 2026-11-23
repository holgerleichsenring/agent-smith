using System.Text;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Commit;

/// <summary>
/// 2026-11-23-933dc, real git: a PoC folder is written as one commit on the shared default branch — a sparse,
/// no-checkout, blob-less clone holding only that folder, the folder replaced by the files (a removed file deleted, a
/// file a nested .gitignore names kept, the marker not), and a PLAIN push that rebases over another folder's concurrent
/// commit instead of overwriting it. Nothing staged reuses the folder's last commit.
/// </summary>
public sealed class PocFolderWriteTests
{
    [Fact]
    public async Task Write_CommitsOnlyTheFolder_FromASparseClone()
    {
        await using var rig = await PocGitRig.CreateAsync();
        if (rig is null) return;
        var clones = new List<string>();
        rig.Wrap = s => new FaultingSandbox(s, step => { if (step.Args?.Contains("clone") == true) clones.Add(string.Join(' ', step.Args)); return null; });

        var sha = await rig.Writer().WriteAsync(new() { Name = "poc-ann" }, rig.Repo, PocGitRig.Folder,
            [File("app.py", "print(1)"), File("static/.gitignore", "*.css\n"), File("static/site.css", "a{}")], "poc todo r1\n\nCo-authored-by: ann", default);

        clones.Should().ContainSingle().Which.Should().Contain("--no-checkout").And.Contain("--sparse").And.Contain("--filter=blob:none");
        Directory.GetFileSystemEntries(rig.Sandboxes[0]).Select(Path.GetFileName).Should().BeEquivalentTo([".git", "pocs"]);
        Directory.Exists(Path.Combine(rig.Sandboxes[0], PocGitRig.OtherFolder)).Should().BeFalse("only the PoC's folder is in the work tree");
        (await rig.OriginAsync("rev-parse", "main")).Should().Be(sha);
        (await rig.OriginAsync("ls-tree", "-r", "--name-only", "main", "--", PocGitRig.Folder)).Split('\n')
            .Should().BeEquivalentTo([$"{PocGitRig.Folder}/app.py", $"{PocGitRig.Folder}/static/.gitignore", $"{PocGitRig.Folder}/static/site.css"]);
        (await rig.OriginAsync("show", "-s", "--format=%B", "main")).Should().Contain("Co-authored-by: ann");
    }

    [Fact]
    public async Task Write_ASecondRevision_DeletesRemovedFiles_AndNothingStagedReusesTheLastCommit()
    {
        await using var rig = await PocGitRig.CreateAsync();
        if (rig is null) return;
        var writer = rig.Writer();
        await writer.WriteAsync(new() { Name = "poc-ann" }, rig.Repo, PocGitRig.Folder, [File("app.py", "1"), File("old.py", "x")], "r1", default);

        var second = await writer.WriteAsync(new() { Name = "poc-ann" }, rig.Repo, PocGitRig.Folder, [File("app.py", "2")], "r2", default);
        var again = await writer.WriteAsync(new() { Name = "poc-ann" }, rig.Repo, PocGitRig.Folder, [File("app.py", "2")], "r3", default);

        (await rig.OriginAsync("ls-tree", "-r", "--name-only", "main", "--", PocGitRig.Folder)).Should().Be($"{PocGitRig.Folder}/app.py");
        again.Should().Be(second);
        (await rig.OriginAsync("rev-list", "--count", "main")).Should().Be("3", "the seed, r1 and r2 — r3 committed nothing");
    }

    [Fact]
    public async Task Write_AConcurrentCommitToAnotherFolder_IsKept_NeverOverwritten()
    {
        await using var rig = await PocGitRig.CreateAsync();
        if (rig is null) return;
        var pushedOnce = false;
        rig.Wrap = s => new FaultingSandbox(s, step =>
        {
            if (pushedOnce || step.Args?.Contains("push") != true) return null;
            pushedOnce = true;
            rig.PushOtherFolderAsync("print('bob 2')\n").GetAwaiter().GetResult();
            return null;
        });

        var sha = await rig.Writer().WriteAsync(new() { Name = "poc-ann" }, rig.Repo, PocGitRig.Folder, [File("app.py", "1")], "r1", default);

        (await rig.OriginAsync("rev-parse", "main")).Should().Be(sha);
        (await rig.OriginAsync("show", $"main:{PocGitRig.OtherFolder}/app.py")).Should().Be("print('bob 2')");
        (await rig.OriginAsync("show", "--name-only", "--format=", "main")).Should().Be($"{PocGitRig.Folder}/app.py");
    }

    [Fact]
    public async Task Write_ASecondRejection_FailsNamingGitsMessage()
    {
        await using var rig = await PocGitRig.CreateAsync();
        if (rig is null) return;
        rig.Wrap = s => new FaultingSandbox(s, step => step.Args?.Contains("push") == true
            ? FaultingSandbox.Failed(step, "! [remote rejected] main (protected branch hook declined)") : null);

        var write = () => rig.Writer().WriteAsync(new() { Name = "poc-ann" }, rig.Repo, PocGitRig.Folder, [File("app.py", "1")], "r1", default);

        (await write.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("protected branch hook declined");
        (await rig.OriginAsync("rev-list", "--count", "main")).Should().Be("1");
    }

    private static ReferenceSetFile File(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));
}
