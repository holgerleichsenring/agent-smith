using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services.Zap;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-25-014d: the registry boundary was already porous — three container
/// images ship compiled in and pass through no trust check at all. They stay
/// outside it deliberately, and this pins the exclusion so a fourth one has to be
/// classified rather than added quietly.
/// </summary>
public sealed class CompiledInToolImagesTests
{
    // The reason, once: a tool-runner image is named HERE, by this repository, and
    // nothing is cloned into it and no agent runs there. The sandbox boundary exists
    // because a toolchain image is named at run time by a model or a catalog profile.
    // Holding these to it would refuse the shipped defaults on a stock installation
    // without making anything safer. An operator repoints a key in their own file.
    private static readonly string[] RecordedExclusions =
    [
        "projectdiscovery/nuclei:latest",
        "stoplight/spectral:6",
    ];

    [Fact]
    public void ToolRunnerImages_AreCoveredOrTheirExclusionIsRecorded()
    {
        var trust = new ImageRegistryTrust();
        var compiledIn = new ToolRunnerConfig().Images.Values
            .Append(ZapSpawner.ScannerImage)
            .ToList();

        compiledIn.Should().NotBeEmpty("the scan must reach the shipped defaults");
        var unclassified = compiledIn
            .Where(image => !trust.Accepts(image) && !RecordedExclusions.Contains(image, StringComparer.Ordinal))
            .ToList();

        unclassified.Should().BeEmpty(
            "a compiled-in container image either sits inside the sandbox registry policy "
            + "or is recorded as deliberately outside it. Add it to RecordedExclusions with "
            + "the reason, or pick an image the policy accepts.\n  "
            + string.Join("\n  ", unclassified));
    }

    [Fact]
    public void TheRecordedExclusions_AreStillTheOnesThatNeedExcluding()
    {
        // An exclusion list that outlives what it excuses stops being a classification.
        var trust = new ImageRegistryTrust();
        var compiledIn = new ToolRunnerConfig().Images.Values
            .Append(ZapSpawner.ScannerImage)
            .ToHashSet(StringComparer.Ordinal);

        RecordedExclusions.Where(e => !compiledIn.Contains(e) || trust.Accepts(e))
            .Should().BeEmpty("remove the entry once the image is gone or the policy covers it");
    }

    [Fact]
    public void TheScannerImage_IsAlreadyInsideTheBoundary()
    {
        // Recorded so the asymmetry is legible: this one needs no exclusion at all.
        new ImageRegistryTrust().Accepts(ZapSpawner.ScannerImage).Should().BeTrue();
    }
}
