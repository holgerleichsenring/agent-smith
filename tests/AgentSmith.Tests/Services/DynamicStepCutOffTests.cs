using System.Text;
using AgentSmith.Application.Services;
using AgentSmith.Tests.Architecture;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Nuclei;
using AgentSmith.Infrastructure.Services.Tools;
using AgentSmith.Infrastructure.Services.Zap;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-08-30-26ed: three consecutive live runs logged "scan completed: N findings in
/// 180s" next to a timeout warning — 180s being the container limit, so the count came
/// from a stopwatch, not from exhaustion. These tests pin the honest report: a step the
/// runner cut off says so, an empty cut-off result is not an empty clean one, and a step
/// that finished reports exactly what it always did.
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class DynamicStepCutOffTests
{
    private const int NucleiLimitSeconds = 45;
    private const int ZapLimitSeconds = 90;

    [Fact]
    public async Task ContainerRunner_KilledAtItsLimit_ReportsCutOffNotAnExitCode()
    {
        var runner = new ProcessToolRunner(NullLogger<ProcessToolRunner>.Instance);

        var killed = await runner.RunAsync(
            new ToolRunRequest("sleep", ["30"], TimeoutSeconds: 1), CancellationToken.None);
        var failedOnItsOwn = await runner.RunAsync(
            new ToolRunRequest("false", [], TimeoutSeconds: 30), CancellationToken.None);

        killed.CutOff.Should().BeTrue("only the runner knows it stopped the tool at its own limit");
        failedOnItsOwn.CutOff.Should().BeFalse();
        failedOnItsOwn.ExitCode.Should().NotBe(0,
            "a non-zero exit is exactly what a cut-off must stay distinguishable from");
    }

    [Fact]
    public async Task DynamicStep_ReachedItsTimeLimit_ReportsCutOffNotCompletion()
    {
        var nuclei = await ScanWithNucleiAsync(CutOffRun());
        var zap = await ScanWithZapAsync(CutOffRun());

        nuclei.Degraded.Should().BeTrue();
        nuclei.DegradedReason.Should().Contain("cut off").And.Contain($"{NucleiLimitSeconds}s");
        zap.Degraded.Should().BeTrue();
        zap.DegradedReason.Should().Contain("cut off").And.Contain($"{ZapLimitSeconds}s");
    }

    [Fact]
    public async Task DynamicStep_EmptyAfterCutOff_IsDistinguishableFromEmptyAfterCompletion()
    {
        var cutOff = await ScanWithZapAsync(CutOffRun());
        var finished = await ScanWithZapAsync(FinishedRun());

        cutOff.Findings.Should().BeEmpty();
        finished.Findings.Should().BeEmpty();

        AccountFor(zap: cutOff).Should().NotBe(AccountFor(zap: finished),
            "an empty result the step never finished producing is not an empty clean one");
        AccountFor(zap: cutOff).Should().Contain("not evidence of a clean target");
    }

    [Fact]
    public async Task DynamicStep_Finished_ReportsCompletionAsBefore()
    {
        var nuclei = await ScanWithNucleiAsync(FinishedRun());
        var zap = await ScanWithZapAsync(FinishedRun());

        nuclei.Degraded.Should().BeFalse();
        nuclei.DegradedReason.Should().BeNull();
        zap.Degraded.Should().BeFalse();
        zap.DegradedReason.Should().BeNull();
        zap.ExitCode.Should().Be(0);
    }

    [Fact]
    public void DynamicStep_ContributedNothing_IsNamedInTheAccount()
    {
        var nuclei = new NucleiResult([Finding()], 12, "");
        var zap = new ZapResult([], 90, "api-scan");

        var account = ApiScanFindingsCompressor.BuildSummary(nuclei, spectral: null, zap);

        account.Should().Contain(DynamicStepAccount.Heading);
        account.Should().Contain("- Nuclei: contributed 1 finding.");
        account.Should().Contain("- ZAP: contributed nothing");
    }

    private static string AccountFor(ZapResult zap) =>
        ApiScanFindingsCompressor.BuildSummary(nuclei: null, spectral: null, zap);

    private static NucleiFinding Finding() =>
        new("t1", "SQLi", "high", "https://example.test/orders", null, null);

    private static async Task<NucleiResult> ScanWithNucleiAsync(ToolResult run)
    {
        var swaggerPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(swaggerPath, """{"paths":{"/orders":{}}}""");
        try
        {
            var spawner = new NucleiSpawner(
                new FixedToolRunner(run),
                new NucleiConfig { ContainerTimeout = NucleiLimitSeconds },
                new ToolRunnerConfig(),
                NullLogger<NucleiSpawner>.Instance);
            return await spawner.ScanAsync("https://example.test", swaggerPath, CancellationToken.None);
        }
        finally { File.Delete(swaggerPath); }
    }

    private static Task<ZapResult> ScanWithZapAsync(ToolResult run)
    {
        var spawner = new ZapSpawner(
            new FixedToolRunner(run),
            new ZapConfig { ContainerTimeout = ZapLimitSeconds },
            new ToolRunnerConfig(),
            NullLogger<ZapSpawner>.Instance);
        return spawner.ScanAsync(
            new ZapScanRequest("https://example.test", "api-scan",
                SwaggerPath: null, AuthToken: null, TimeoutSeconds: ZapLimitSeconds),
            CancellationToken.None);
    }

    /// <summary>What DockerToolRunner returns for a container it killed at the limit.</summary>
    private static ToolResult CutOffRun() => new("", "Timeout", null, 1, ZapLimitSeconds, CutOff: true);

    private static ToolResult FinishedRun() => new("", "", null, 0, 3);

    private sealed class FixedToolRunner(ToolResult result) : IToolRunner
    {
        public Task<ToolResult> RunAsync(ToolRunRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
