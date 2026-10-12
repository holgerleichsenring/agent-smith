using System.Diagnostics;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-10-12-6bfaa: the local provider reads a NAMED branch's committed state through git, against
/// a real repository — the ticket branch carries a context the checked-out default branch does not.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class LocalSourceProviderBranchReadTests : IDisposable
{
    private const string Ticket = "agent-smith/4711";
    private const string ContextYaml = ".agentsmith/contexts/default/context.yaml";

    private readonly string _repoPath = Path.Combine(Path.GetTempPath(), $"agentsmith-6bfaa-{Guid.NewGuid():N}");

    public LocalSourceProviderBranchReadTests()
    {
        Directory.CreateDirectory(_repoPath);
        Git("init", "--initial-branch=main");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
        Commit("README.md", "base");
        Git("checkout", "-b", Ticket);
        Commit(ContextYaml, "stack:\n  lang: scala\n");
        Git("checkout", "main");
    }

    public void Dispose()
    {
        try { Directory.Delete(_repoPath, recursive: true); }
        catch (IOException) { /* a temp dir the OS still holds is not this test's problem */ }
    }

    [Fact]
    public async Task LocalBranchRead_ReadsTheCommittedBranch_NotTheWorkingTree()
    {
        var sut = new LocalSourceProvider(_repoPath, "main");
        var branch = new BranchName(Ticket);

        (await sut.BranchExistsAsync(branch, CancellationToken.None)).Should().BeTrue();
        (await sut.ListDirectoryAsync(".agentsmith/contexts", branch, CancellationToken.None))
            .Should().Equal("default");
        (await sut.TryReadFileAsync(ContextYaml, branch, CancellationToken.None))
            .Should().Be("stack:\n  lang: scala\n");
        (await sut.TryReadFileAsync(ContextYaml, CancellationToken.None))
            .Should().BeNull("the working tree has main checked out, which carries no context");
    }

    [Fact]
    public async Task LocalBranchRead_AbsentBranchAndPath_AnswerAbsent()
    {
        var sut = new LocalSourceProvider(_repoPath, "main");

        (await sut.BranchExistsAsync(new BranchName("agent-smith/none"), CancellationToken.None)).Should().BeFalse();
        (await sut.ListDirectoryAsync(".agentsmith/contexts", new BranchName("main"), CancellationToken.None))
            .Should().BeEmpty();
        (await sut.TryReadFileAsync(ContextYaml, new BranchName("main"), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task LocalBranchRead_NotAGitRepository_HasNoBranches()
    {
        var sut = new LocalSourceProvider(Path.Combine(_repoPath, "does-not-exist"), "main");

        (await sut.BranchExistsAsync(new BranchName(Ticket), CancellationToken.None)).Should().BeFalse();
    }

    private void Commit(string relativePath, string content)
    {
        var full = Path.Combine(_repoPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        Git("add", "-A");
        Git("commit", "-m", $"add {relativePath}");
    }

    private void Git(params string[] arguments)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var process = Process.Start(psi)!;
        process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, $"git {string.Join(' ', arguments)} failed: {stderr}");
    }
}
