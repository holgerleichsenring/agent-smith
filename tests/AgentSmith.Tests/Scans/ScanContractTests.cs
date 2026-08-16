using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// p0429: a scan states what it is looking for BEFORE it looks. Without a stated target
/// there is no "satisfied" and no way to tell a MISS from a NON-GOAL — a dependency
/// audit that failed to restore made the run report clean.
/// </summary>
public sealed class ScanContractTests
{
    [Fact]
    public void ScanContract_IsRatifiedBeforeTheFirstScanner()
    {
        var preset = PipelinePresets.SecurityScan;
        var ratify = preset.ToList().IndexOf(CommandNames.RatifyScanContract);

        ratify.Should().BeGreaterThan(-1, "a scan must state its targets");
        ratify.Should().BeLessThan(preset.ToList().IndexOf(CommandNames.StaticPatternScan),
            "a contract written after the findings are in can never report a miss");
    }

    [Fact]
    public void ScanContract_NamesTheScannersThePresetDeclares()
    {
        var contract = new ScanContractCatalogue().For("security-scan");

        contract.Criteria.Select(c => c.AnsweredBy).Should().Contain(
            [CommandNames.StaticPatternScan, CommandNames.GitHistoryScan,
             CommandNames.DependencyAudit, CommandNames.SubstantiateFindings]);
        contract.Criteria.Should().NotContain(c => c.AnsweredBy == CommandNames.SpawnZap,
            "a step this preset does not run is not something it claims to look for");
    }

    [Fact]
    public void ScanContract_ForAPipelineWithNoScanSteps_ClaimsNothing()
    {
        // p0421's rule, kept: a run that ratifies nothing is not judged. Inventing a
        // requirement nobody stated is how the old gate failed runs that had delivered.
        new ScanContractCatalogue().For(PipelinePresets.CodeName).Criteria.Should().BeEmpty();
        new ScanContractCatalogue().For("no-such-pipeline").Criteria.Should().BeEmpty();
    }

    [Fact]
    public async Task RatifyScanContract_PutsTheCriteriaWhereTheOneGateReadsThem()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineName, "security-scan");

        var result = await Handler().ExecuteAsync(
            new RatifyScanContractContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        AcceptanceCriteria.For(pipeline).Should().NotBeEmpty(
            "the scan's contract is the run's contract — the same accessor the coding "
            + "pipeline's gate reads, not a second one");
    }

    [Fact]
    public async Task RatifyScanContract_OnAPipelineThatClaimsNothing_SetsNoContract()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.CodeName);

        await Handler().ExecuteAsync(new RatifyScanContractContext(pipeline), CancellationToken.None);

        pipeline.TryGet<ScanContract>(ContextKeys.ScanContract, out _).Should().BeFalse();
    }

    private static RatifyScanContractHandler Handler() =>
        new(new ScanContractCatalogue(), NullLogger<RatifyScanContractHandler>.Instance);
}
