using System.Diagnostics;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0496: a real git remote with a base branch and a work branch that was cut from an
/// EARLIER state of it — the shape three live runs died on. Mocking ISandbox would only
/// prove the code issued a string; the whole question here is what git does with it.
/// </summary>
internal sealed class GitRemoteFixture : IAsyncDisposable
{
    public const string BaseBranch = "main";
    public const string SharedFile = "shared.txt";
    public const string BaseOnlyFile = "arrived-on-the-base.txt";
    public const string WorkOnlyFile = "arrived-on-the-branch.txt";

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
    public static GitRemoteFixture Create(string? workBranch, bool conflicting = false)
    {
        var fixture = new GitRemoteFixture(FixtureWorkdir.CreateEmpty());
        Directory.CreateDirectory(fixture.RemotePath);
        Directory.CreateDirectory(fixture.WorkPath);
        var remote = fixture.RemotePath;

        Git(remote, "init", "-b", BaseBranch);
        Git(remote, "config", "user.email", "fixture@example.com");
        Git(remote, "config", "user.name", "fixture");
        Commit(remote, SharedFile, "one\n", "base v1");

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
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {error}");
    }

    public ValueTask DisposeAsync() => _root.DisposeAsync();
}
