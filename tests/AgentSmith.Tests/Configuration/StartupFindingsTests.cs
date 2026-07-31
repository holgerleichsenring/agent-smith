using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0391a: the findings list is read by an operator, so it must show WHAT is broken —
/// not how often the server noticed it, and not faults that have since been fixed.
/// </summary>
public class StartupFindingsTests
{
    private readonly StartupFindings _sut = new();

    [Fact]
    public void Record_SameUnitTwice_KeepsOneEntryWithTheNewestReason()
    {
        _sut.Record(Blocking("first"));
        _sut.Record(Blocking("second"));

        _sut.All.Should().ContainSingle().Which.Reason.Should().Be("second");
    }

    [Fact]
    public void Record_DifferentUnits_KeepsInsertionOrder()
    {
        _sut.Record(Blocking("a", trigger: "jira_trigger"));
        _sut.Record(Blocking("b", trigger: "github_trigger"));

        _sut.All.Select(f => f.Reason).Should().Equal("a", "b");
    }

    [Fact]
    public void Clear_Subsystem_RemovesOnlyThatSubsystemsFindings()
    {
        _sut.Record(Blocking("config"));
        _sut.Record(new StartupFinding(
            StartupSubsystems.Redis, StartupFindingSeverity.Blocking, "redis down"));

        _sut.Clear(StartupSubsystems.Configuration);

        _sut.All.Should().ContainSingle().Which.Subsystem.Should().Be(StartupSubsystems.Redis);
    }

    [Fact]
    public void IsTriggerBlocked_BlockingFindingOnThatTrigger_IsTrue()
    {
        _sut.Record(Blocking("broken", trigger: "github_trigger"));

        _sut.IsTriggerBlocked("proj", "github_trigger").Should().BeTrue();
        _sut.IsTriggerBlocked("proj", "jira_trigger").Should().BeFalse();
        _sut.IsTriggerBlocked("other", "github_trigger").Should().BeFalse();
    }

    [Fact]
    public void IsTriggerBlocked_ProjectWideFinding_BlocksEveryTriggerOfThatProject()
    {
        _sut.Record(Blocking("project broken", trigger: null));

        _sut.IsTriggerBlocked("proj", "github_trigger").Should().BeTrue();
        _sut.IsTriggerBlocked("proj", "jira_trigger").Should().BeTrue();
    }

    [Fact]
    public void IsTriggerBlocked_AdvisoryFinding_DoesNotBlock()
    {
        _sut.Record(new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Advisory, "heads up",
            "proj", "github_trigger", "trigger_statuses"));

        _sut.IsTriggerBlocked("proj", "github_trigger").Should().BeFalse();
    }

    [Fact]
    public void BlockingReason_NamesTheReasonOfTheBlockingFinding()
    {
        _sut.Record(Blocking("no park status", trigger: "github_trigger"));

        _sut.BlockingReason("proj", "github_trigger").Should().Be("no park status");
    }

    private static StartupFinding Blocking(string reason, string? trigger = "github_trigger") =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, reason,
            "proj", trigger, "needs_clarification_status");
}
