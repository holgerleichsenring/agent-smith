using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>2026-11-17-166aa: a failure on a project without failed_status leaves the ticket in its tracker status, so
/// the claim door holds it from the poller, a webhook and the pump; a person's start passes, and an end that lost
/// only its process (owed work) stays reclaimable.</summary>
public sealed class LeftInPlaceGateTests
{
    [Theory]
    [InlineData(RunStatuses.Failed, null)]
    [InlineData("cancelled", "operator")]
    [InlineData("cancelled", "watchdog-wall-time")]
    public async Task Retrigger_EndLeftInPlace_WithoutFailedStatus_IsRefusedNamingTheKey(string status, string? reason)
    {
        var refusal = await Gate(Attempt(status, reason)).RefusalAsync(Request(), Config(failed: null), CancellationToken.None);

        refusal!.Rejection.Should().Be(ClaimRejectionReason.TicketLeftInPlace);
        refusal.Error.Should().Contain("run-1").And.Contain($"ended {status}").And.Contain("project 'my-project'")
            .And.Contain("failed_status").And.Contain("Retry");
    }

    [Theory]
    [InlineData(OwedWorkCancel.Interrupted)]
    [InlineData(OwedWorkCancel.StaleLeaseReaped)]
    public async Task Retrigger_OwedWorkCancel_IsClaimed(string reason) =>
        (await Gate(Attempt("cancelled", reason)).RefusalAsync(Request(), Config(failed: null), CancellationToken.None))
            .Should().BeNull("a lost process owes the work, so the ticket stays reclaimable");

    [Fact]
    public async Task Retrigger_FailedWithFailedStatusConfigured_IsClaimed() =>
        (await Gate(Attempt(RunStatuses.Failed, null)).RefusalAsync(Request(), Config(failed: "Failed"), CancellationToken.None))
            .Should().BeNull("the failure moved the ticket out of its trigger status");

    [Fact]
    public async Task Retrigger_SuccessHead_IsClaimed() =>
        (await Gate(Attempt(RunStatuses.Success, null)).RefusalAsync(Request(), Config(failed: null), CancellationToken.None))
            .Should().BeNull();

    [Fact]
    public async Task PersonsStart_EndLeftInPlace_IsAdmitted()
    {
        var byName = Request() with { InitialContext = new() { [ContextKeys.RequestedByName] = true } };

        (await Gate(Attempt(RunStatuses.Failed, null)).RefusalAsync(byName, Config(failed: null), CancellationToken.None))
            .Should().BeNull("Retry and a reply start the ticket by name");
    }

    [Fact]
    public async Task Retrigger_PreconditionStop_KeepsThePreconditionSentence()
    {
        var held = Attempt(RunStatuses.Failed, null) with { StoppedOnPrecondition = true, EndKind = RunEndKinds.Preflight };

        var refusal = await Gate(held).RefusalAsync(Request(), Config(failed: null), CancellationToken.None);

        refusal!.Rejection.Should().Be(ClaimRejectionReason.TicketStuckOnPrecondition);
    }

    [Fact]
    public void LeftInPlaceEnd_FailureAndAPersonsCancelHold_SuccessShortfallAndOwedWorkDoNot()
    {
        LeftInPlaceEnd.Ends(RunStatuses.Failed, null).Should().BeTrue();
        LeftInPlaceEnd.Ends("cancelled", "operator").Should().BeTrue();
        LeftInPlaceEnd.Ends("cancelled", null).Should().BeTrue();
        LeftInPlaceEnd.Ends("cancelled", OwedWorkCancel.Interrupted).Should().BeFalse();
        LeftInPlaceEnd.Ends("cancelled", OwedWorkCancel.StaleLeaseReaped).Should().BeFalse();
        LeftInPlaceEnd.Ends(RunStatuses.Success, null).Should().BeFalse();
        LeftInPlaceEnd.Ends(RunStatuses.Shortfall, null).Should().BeFalse();
        OwedWorkCancel.IsOwed("operator").Should().BeFalse();
    }

    private static PreviousAttempt Attempt(string status, string? reason) =>
        new("run-1", status, DateTimeOffset.UtcNow, true) { CancelReason = reason };

    private static BoundaryStopGate Gate(PreviousAttempt latest)
    {
        var attempts = new Mock<IPreviousAttemptReader>();
        attempts.Setup(a => a.LatestAsync("my-project", "42", null, It.IsAny<CancellationToken>())).ReturnsAsync(latest);
        return new BoundaryStopGate(attempts.Object);
    }

    private static ClaimRequest Request() => new("GitHub", "my-project", new TicketId("42"), "code");

    private static AgentSmithConfig Config(string? failed) => new()
    {
        Projects = new()
        {
            ["my-project"] = new ResolvedProject
            {
                Name = "my-project", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
                GithubTrigger = new WebhookTriggerConfig { DefaultPipeline = "code", DoneStatus = "In Review", FailedStatus = failed },
            },
        },
    };
}
