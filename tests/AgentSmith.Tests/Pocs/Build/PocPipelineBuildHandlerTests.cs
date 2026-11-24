using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Pocs.Build;

/// <summary>2026-11-23-933dd: the build step over a faked host and registry — idempotent on retry, the run id stored
/// before the first read, blocked at the image stage only on the gate's failure plus a blocking finding.</summary>
public sealed class PocPipelineBuildHandlerTests
{
    private readonly PocBuildRig _rig = new();

    [Fact]
    public async Task Execute_TagAlreadyInTheRegistry_StartsNoPipelineAndTakesItsDigest()
    {
        _rig.Tagged = PocBuildRig.Digest;
        var context = _rig.Context();

        var result = await _rig.Handler().ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        _rig.Starts.Should().BeEmpty();
        _rig.Store.Rows[1].State.Should().Be(PocRevisionStates.Built);
        context.Pipeline.Get<string>(ContextKeys.PocImageDigest).Should().Be(PocBuildRig.Digest);
    }

    [Fact]
    public async Task Execute_Green_StoresTheRunBeforeReadingIt_KeepsNotes_AndVerifiesTheDigest()
    {
        _rig.States("run-1", CiStates.Pending, CiStates.Green);
        _rig.Report("run-1", "0", PocBuildRig.Vulnerability("CVE-2024-7", 7.5, severity: "CRITICAL"));
        _rig.Artifacts["run-1/digest.txt"] = PocBuildRig.Digest + "\n";
        _rig.Runner.Setup(r => r.ReadAsync("run-1", It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => _rig.Store.Rows[1].PipelineRunId.Should().Be("run-1", "the run id is stored before the first read"))
            .ReturnsAsync(() => { _rig.Tagged = PocBuildRig.Digest; return new PipelineRunState(CiStates.Green, "u", []); });

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.IsSuccess.Should().BeTrue(result.Message);
        var row = _rig.Store.Rows[1];
        (row.State, row.Digest).Should().Be((PocRevisionStates.Built, PocBuildRig.Digest));
        PocScanVerdict.FromJson(row.ImageVerdictJson)!.Notes.Should().ContainSingle(n => n.Id == "trivy:CVE-2024-7|openssl|1.0");
        var inputs = _rig.Starts.Single().Inputs;
        inputs["sha"].Should().Be(PocBuildRig.Sha);
        inputs["folder"].Should().Be("pocs/poc-ann/todo");
        inputs["ignore_unfixed"].Should().Be("false");
        inputs["secret_rules"].Split(',').Should().Contain(["aws-access-key-id", "private-key", "stripe-secret-token"])
            .And.NotContain("stripe-publishable-token");
        _rig.Starts.Single().Pipeline.Should().Be("poc-build.yml");
    }

    [Fact]
    public async Task Execute_GreenButTheRegistryResolvesAnotherDigest_IsRefusedNamingBoth()
    {
        _rig.States("run-1", CiStates.Green);
        _rig.Report("run-1", "0");
        _rig.Artifacts["run-1/digest.txt"] = PocBuildRig.Digest;
        var other = "sha256:" + new string('2', 64);
        _rig.Runner.Setup(r => r.ReadAsync("run-1", It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { _rig.Tagged = other; return new PipelineRunState(CiStates.Green, "u", []); });

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(PocBuildRig.Digest).And.Contain(other);
        _rig.Store.Rows[1].State.Should().Be(PocRevisionStates.Validated);
    }

    [Fact]
    public async Task Execute_StoredPendingRun_IsFollowedNotRestarted()
    {
        _rig.Store.Rows[1].PipelineRunId = "run-9";
        _rig.States("run-9", CiStates.Pending, CiStates.Red);
        _rig.Report("run-9", "1", PocBuildRig.Vulnerability("CVE-2024-1", 9.8));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        _rig.Starts.Should().BeEmpty();
        result.IsSuccess.Should().BeFalse();
        _rig.Store.Rows[1].BlockedStage.Should().Be(PocScanStages.Image);
    }

    [Fact]
    public async Task Execute_AHostThatStartsWithoutNamingTheRun_Fails()
    {
        _rig.Runner.Setup(r => r.StartAsync(It.IsAny<PipelineRunOrder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineRunStart(null, "https://github.com/x/actions"));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("return_run_details");
    }

    [Theory]
    [InlineData(PocStates.Importing, PocStates.Blocked)]
    [InlineData(PocStates.Ready, PocStates.Ready)]
    public async Task Execute_RedWithTheGateFailedOnACritical_BlocksAtTheImageStage(string before, string after)
    {
        _rig.Store.PocState = before;
        _rig.States("run-1", CiStates.Red);
        _rig.Report("run-1", "1", PocBuildRig.Vulnerability("CVE-2024-1", 9.8, severity: "LOW"), PocBuildRig.Vulnerability("CVE-2024-2", 5.0));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("blocked by the image scan");
        var row = _rig.Store.Rows[1];
        (row.State, row.BlockedStage).Should().Be((PocRevisionStates.Blocked, PocScanStages.Image));
        var verdict = PocScanVerdict.FromJson(row.ImageVerdictJson)!;
        verdict.Blocks.Should().ContainSingle(b => b.Id == "trivy:CVE-2024-1|openssl|1.0");
        verdict.Overrulable.Should().BeTrue();
        _rig.Store.PocState.Should().Be(after);
    }

    [Theory]
    [InlineData("0", 9.8)]
    [InlineData("1", 8.9)]
    public async Task Execute_RedWithoutABlockingReport_FailsWithTheRunsFailures(string gate, double score)
    {
        _rig.States("run-1", CiStates.Red);
        _rig.Report("run-1", gate, PocBuildRig.Vulnerability("CVE-2024-1", score));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("registry answered 503");
        _rig.Store.Rows[1].State.Should().Be(PocRevisionStates.Validated);
        _rig.Store.Rows[1].PipelineRunId.Should().Be("run-1", "the id stays; the next retry starts afresh");
    }

    [Fact]
    public async Task Execute_AnUnfixedCriticalWithTheRuleOff_IsNoBlock()
    {
        _rig.Pocs.Scan.UnfixedCriticalBlock = false;
        _rig.States("run-1", CiStates.Red);
        _rig.Report("run-1", "1", PocBuildRig.Vulnerability("CVE-2024-1", 9.8, fixedVersion: ""));

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.Message.Should().Contain("registry answered 503");
        _rig.Store.Rows[1].BlockedStage.Should().BeNull();
        _rig.Starts.Single().Inputs["ignore_unfixed"].Should().Be("true");
    }

    [Fact]
    public async Task Execute_RetryAfterARedRunWithoutABlock_ClearsItAndStartsAFreshRun()
    {
        _rig.Store.Rows[1].PipelineRunId = "run-old";
        _rig.States("run-old", CiStates.Red);
        _rig.States("run-1", CiStates.Green);
        _rig.Report("run-1", "0");
        _rig.Artifacts["run-1/digest.txt"] = PocBuildRig.Digest;
        _rig.Runner.Setup(r => r.ReadAsync("run-1", It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { _rig.Tagged = PocBuildRig.Digest; return new PipelineRunState(CiStates.Green, "u", []); });

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.IsSuccess.Should().BeTrue(result.Message);
        _rig.Store.StoredRuns.Should().Equal(null, "run-1");
    }

    [Theory]
    [InlineData(CiStates.Red)]
    [InlineData(CiStates.Green)]
    public async Task Execute_AReportOverReportKb_FailsNamingTheKey(string state)
    {
        _rig.Pocs.Targets[0].Build.ReportKb = 1;
        _rig.States("run-1", state);
        _rig.Artifacts["run-1/report.json"] = new string(' ', 3000);

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("the scan report is 3 KB").And.Contain("report_kb").And.Contain("raise it");
    }

    [Fact]
    public async Task Execute_AHostThatNeverFinishes_FailsAtTheDeadlineOfTheFourKeys()
    {
        _rig.States("run-1", CiStates.Pending);

        var result = await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        result.Message.Should().Contain("within 23 minutes").And.Contain("queue_minutes").And.Contain("push_timeout_minutes")
            .And.Contain("never finished the build");
        _rig.Clock.Waits.Should().BeGreaterThan(80, "23 minutes polled every 15 seconds");
    }

    [Fact]
    public async Task Execute_OnGitLab_SendsThePocVariables()
    {
        _rig.Host = RepoType.GitLab;
        _rig.States("run-1", CiStates.Red);

        await _rig.Handler().ExecuteAsync(_rig.Context(), default);

        var order = _rig.Starts.Single();
        order.Pipeline.Should().BeNull();
        order.Inputs.Keys.Should().BeEquivalentTo(["POC_NAME", "POC_FOLDER", "POC_SHA", "POC_TRIVY_IGNORE", "POC_IGNORE_UNFIXED", "POC_SECRET_RULES"]);
        order.Inputs["POC_TRIVY_IGNORE"].Should().Be("none");
    }
}
