using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// p0428: a ticket branch carrying an earlier run's commits is REPORTED. p0112 and p0360
/// create exactly that state on purpose, so refusing the run would refuse the framework's
/// own resume.
/// </summary>
public sealed class BranchStateCheckTests
{
    [Fact]
    public async Task ABranchCarryingEarlierWork_WarnsAndNeverFails()
    {
        var log = string.Join("\n",
            SandboxGitIdentity.Email, SandboxGitIdentity.Email, "someone@example.com");

        var finding = await new BranchStateCheck(NullLogger<BranchStateCheck>.Instance)
            .RunAsync(PipelineWith(new ScriptedSandbox(log)), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Warn);
        finding.Message.Should().Contain("api: 2").And.Contain("agentsmith/4242");
        finding.Message.Should().Contain("skipped");
    }

    [Fact]
    public async Task ABranchWithOnlyHumanCommits_Passes()
    {
        var finding = await new BranchStateCheck(NullLogger<BranchStateCheck>.Instance)
            .RunAsync(PipelineWith(new ScriptedSandbox("someone@example.com")), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public async Task AHistoryThatCannotBeRead_Passes()
    {
        var finding = await new BranchStateCheck(NullLogger<BranchStateCheck>.Instance)
            .RunAsync(PipelineWith(new ScriptedSandbox("fatal: not a git repository", 128)),
                CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
    }

    [Fact]
    public void CountAuthored_CountsOnlyTheFrameworkIdentity()
    {
        BranchStateCheck.CountAuthored($"{SandboxGitIdentity.Email}\nhuman@example.com\n")
            .Should().Be(1);
        BranchStateCheck.CountAuthored(string.Empty).Should().Be(0);
    }

    private static PipelineContext PipelineWith(ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.CheckoutBranch, "agentsmith/4242");
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["api"] = sandbox });
        return pipeline;
    }
}
