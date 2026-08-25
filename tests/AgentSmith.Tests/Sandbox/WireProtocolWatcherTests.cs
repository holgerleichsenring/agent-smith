using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-25-0d01: the reader the schema version never had. It sat on all three wire
/// records, was written at about forty construction sites, and was read at none.
/// </summary>
public sealed class WireProtocolWatcherTests
{
    [Fact]
    public void Protocol_AVersionOutsideTheWindow_IsANamedFinding()
    {
        var findings = new StartupFindings();

        Sut(findings).Observe(WireProtocol.Current + 1);

        var finding = findings.All.Should().ContainSingle().Subject;
        finding.Subsystem.Should().Be(StartupSubsystems.SandboxAgent);
        finding.Field.Should().Be("schema_version");
        finding.Reason.Should().Contain((WireProtocol.Current + 1).ToString())
            .And.Contain(WireProtocol.Window);
    }

    [Fact]
    public void Protocol_AVersionInsideTheWindow_ProducesNoFinding()
    {
        var findings = new StartupFindings();

        Sut(findings).Observe(WireProtocol.Current);

        findings.All.Should().BeEmpty();
    }

    [Fact]
    public void Protocol_AVersionOutsideTheWindow_IsAdvisoryAndRefusesNothing()
    {
        var findings = new StartupFindings();

        Sut(findings).Observe(0);

        var finding = findings.All.Should().ContainSingle().Subject;
        finding.Severity.Should().Be(StartupFindingSeverity.Advisory,
            "a run whose agent was pinned to an older release is a RUNNING run, and refusing "
            + "its results would convert a difference into a failure it had not caused");
        finding.IsBlocking.Should().BeFalse();
    }

    [Fact]
    public void Protocol_TheSameDifferenceSeenRepeatedly_IsReportedOnce()
    {
        var findings = new StartupFindings();
        var sut = Sut(findings);

        for (var i = 0; i < 50; i++) sut.Observe(WireProtocol.Current + 1);

        findings.All.Should().ContainSingle("every step produces a result, and an operator "
            + "wants a list of what is wrong, not a list of how often it was noticed");
    }

    private static WireProtocolWatcher Sut(StartupFindings findings) =>
        new(findings, NullLogger<WireProtocolWatcher>.Instance);
}
