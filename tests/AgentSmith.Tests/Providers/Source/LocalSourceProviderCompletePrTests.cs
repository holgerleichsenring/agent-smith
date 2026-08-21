using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0490: a local repository has no pull request server, so "completing the pull
/// request" is moving the default branch onto the branch the run committed. Driven
/// against a REAL git repository, because the whole claim is about what the refs say
/// afterwards and about git's own refusal of a non-fast-forward — a mocked process
/// could only restate the code.
/// </summary>
public sealed class LocalSourceProviderCompletePrTests : IDisposable
{
    private const string WorkBranch = "agentsmith/init";
    private readonly string _repoPath = Path.Combine(
        Path.GetTempPath(), $"agentsmith-p0490-{Guid.NewGuid():N}");

    public LocalSourceProviderCompletePrTests()
    {
        Directory.CreateDirectory(_repoPath);
        Git("init", "--initial-branch=main");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
        Commit("README.md", "base");
    }

    public void Dispose()
    {
        try { Directory.Delete(_repoPath, recursive: true); }
        catch (IOException) { /* a temp dir the OS still holds is not this test's problem */ }
    }

    [Fact]
    public async Task LocalSourceProvider_CompletePullRequest_FastForwardsTheDefaultBranch()
    {
        Git("checkout", "-b", WorkBranch);
        Commit(".agentsmith/context.yaml", "stack: csharp");
        var initHead = Rev(WorkBranch);
        Rev("main").Should().NotBe(initHead, "the default branch has not moved yet");

        var completion = await CreateSut().CompletePullRequestAsync(
            "Local repository - no PR created", new BranchName(WorkBranch), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
        Rev("main").Should().Be(initHead, "the default branch now carries the init commit");
    }

    [Fact]
    public async Task LocalSourceProvider_CompletePullRequest_DefaultBranchMovedOn_IsRefused_NotForced()
    {
        Git("checkout", "-b", WorkBranch);
        Commit(".agentsmith/context.yaml", "stack: csharp");
        Git("checkout", "main");
        Commit("other.txt", "someone else committed here");
        var mainHead = Rev("main");
        Git("checkout", WorkBranch);

        var completion = await CreateSut().CompletePullRequestAsync(
            "Local repository - no PR created", new BranchName(WorkBranch), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().Contain("fast-forward");
        Rev("main").Should().Be(mainHead, "a refused completion leaves the default branch alone");
    }

    [Fact]
    public async Task LocalSourceProvider_CompletePullRequest_ConfiguredDefaultBranch_IsTheOneMoved()
    {
        Git("branch", "trunk");
        Git("checkout", "-b", WorkBranch);
        Commit(".agentsmith/context.yaml", "stack: csharp");

        var completion = await new LocalSourceProvider(_repoPath, "trunk").CompletePullRequestAsync(
            "Local repository - no PR created", new BranchName(WorkBranch), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
        Rev("trunk").Should().Be(Rev(WorkBranch));
        Rev("main").Should().NotBe(Rev(WorkBranch), "only the configured default branch moves");
    }

    private LocalSourceProvider CreateSut() => new(_repoPath, "main");

    private string Rev(string branch) => Git("rev-parse", branch).Trim();

    private void Commit(string relativePath, string content)
    {
        var full = Path.Combine(_repoPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        Git("add", "-A");
        Git("commit", "-m", $"add {relativePath}");
    }

    private string Git(params string[] arguments)
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
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, $"git {string.Join(' ', arguments)} failed: {stderr}");
        return stdout;
    }
}
