using System.Diagnostics;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0496: a real git remote with a base branch and a work branch that was cut from an
/// EARLIER state of it — the shape three live runs died on. Mocking ISandbox would only
/// prove the code issued a string; the whole question here is what git does with it.
/// <para>
/// 2026-09-13-5cdf: it can also carry a feature RUNG between the base and the work
/// branch, so "the branch was cut from the rung, not from the default branch" is asked of
/// git rather than of a recorded argument list.
/// </para>
/// </summary>
internal sealed class GitRemoteFixture : IAsyncDisposable
{
    public const string BaseBranch = "main";
    public const string SharedFile = "shared.txt";
    public const string BaseOnlyFile = "arrived-on-the-base.txt";
    public const string WorkOnlyFile = "arrived-on-the-branch.txt";
    public const string RungOnlyFile = "arrived-on-the-rung.txt";
    public const string RungLaterFile = "arrived-on-the-rung-later.txt";

    private readonly FixtureWorkdir _root;

    public string RemotePath { get; }
    public string WorkPath { get; }

    private GitRemoteFixture(FixtureWorkdir root)
    {
        _root = root;
        RemotePath = Path.Combine(root.Path, "remote");
        WorkPath = Path.Combine(root.Path, "work");
    }

    /// <param name="workBranch">The branch to create on the remote, or null for a first run.</param>
    /// <param name="conflicting">
    /// True: the later base commit rewrites the same line the work branch rewrote.
    /// </param>
    /// <param name="rung">
    /// A feature branch cut from the base, which the work branch is then cut from — null
    /// for a remote that has no feature branch yet, which is what the first slice of a
    /// feature finds and 2026-09-13-35a4 publishes into.
    /// </param>
    public static GitRemoteFixture Create(
        string? workBranch, bool conflicting = false, string? rung = null)
    {
        var fixture = new GitRemoteFixture(FixtureWorkdir.CreateEmpty());
        Directory.CreateDirectory(fixture.RemotePath);
        Directory.CreateDirectory(fixture.WorkPath);
        var remote = fixture.RemotePath;

        Git(remote, "init", "-b", BaseBranch);
        Git(remote, "config", "user.email", "fixture@example.com");
        Git(remote, "config", "user.name", "fixture");
        Commit(remote, SharedFile, "one\n", "base v1");

        if (rung is not null)
        {
            Git(remote, "checkout", "-b", rung);
            Commit(remote, RungOnlyFile, "arrived on the rung\n", "rung v1");
        }

        if (workBranch is not null)
        {
            Git(remote, "checkout", "-b", workBranch);
            if (conflicting) Commit(remote, SharedFile, "the branch rewrote it\n", "work v1");
            else Commit(remote, WorkOnlyFile, "work\n", "work v1");
            Git(remote, "checkout", BaseBranch);
        }
        return fixture;
    }

    /// <summary>The commit the work branch was cut before — the file the operator was looking at.</summary>
    public void AdvanceBase(bool conflicting = false)
    {
        if (conflicting) Commit(RemotePath, SharedFile, "the base rewrote it\n", "base v2");
        else Commit(RemotePath, BaseOnlyFile, "arrived after the branch was cut\n", "base v2");
    }

    /// <summary>A commit that lands on the RUNG after the work branch was cut from it.</summary>
    public void AdvanceRung(string rung)
    {
        Git(RemotePath, "checkout", rung);
        Commit(RemotePath, RungLaterFile, "arrived on the rung after the branch was cut\n", "rung v2");
        Git(RemotePath, "checkout", BaseBranch);
    }

    /// <summary>
    /// 2026-09-13-35a4: a second slice's own full clone, taken BEFORE the feature branch
    /// exists — the window the creation race lives in. Two slices with no predecessor edge
    /// between them start together, so both clones predate the rung and both will try to
    /// create it.
    /// </summary>
    public string NewClone(string name)
    {
        var path = Path.Combine(_root.Path, name);
        Git(_root.Path, "clone", RemotePath, path);
        Git(path, "config", "user.email", "fixture@example.com");
        Git(path, "config", "user.name", "fixture");
        return path;
    }

    /// <summary>What the remote says, without throwing — for asking about refs that may not exist.</summary>
    public (int Exit, string Output) AskRemote(params string[] args) => Run(RemotePath, args);

    public string ReadWorkFile(string relativePath) =>
        File.ReadAllText(Path.Combine(WorkPath, relativePath));

    public bool WorkFileExists(string relativePath) =>
        File.Exists(Path.Combine(WorkPath, relativePath));

    private static void Commit(string repo, string file, string content, string message)
    {
        File.WriteAllText(Path.Combine(repo, file), content);
        Git(repo, "add", file);
        Git(repo, "commit", "-m", message);
    }

    private static void Git(string workingDirectory, params string[] args)
    {
        var (exit, output) = Run(workingDirectory, args);
        if (exit != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {output}");
    }

    private static (int Exit, string Output) Run(string workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        var error = process.StandardError.ReadToEnd();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error);
    }

    public ValueTask DisposeAsync() => _root.DisposeAsync();
}
