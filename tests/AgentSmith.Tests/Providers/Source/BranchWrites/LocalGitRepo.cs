using System.Diagnostics;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source.BranchWrites;

/// <summary>2026-11-23-c81ea: a throwaway git repository on disk with a main branch, for the local write and listing.</summary>
internal sealed class LocalGitRepo : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"agentsmith-c81ea-{Guid.NewGuid():N}");

    public LocalGitRepo()
    {
        Directory.CreateDirectory(Path);
        Git("init", "--initial-branch=main");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
    }

    public void Commit(string relativePath, string content)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        Git("add", "-A");
        Git("commit", "-m", $"add {relativePath}");
    }

    public string Git(params string[] arguments)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, $"git {string.Join(' ', arguments)} failed: {stderr}");
        return stdout.Trim();
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { /* a temp dir the OS still holds is not this test's problem */ }
    }
}
