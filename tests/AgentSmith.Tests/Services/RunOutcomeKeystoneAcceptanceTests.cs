using System;
using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0340: the keystone now gates on the ratified acceptance contract. A change that
// built and tested green but left the negotiated criteria unmet is NOT a success —
// this is the exact "1-line csproj vs. a whole migration" hole the phase closes.
public sealed class RunOutcomeKeystoneAcceptanceTests
{
    private static readonly string[] TwoCriteria =
    {
        "All MediatR usages replaced by Mediator",
        "All MassTransit usages replaced by Wolverine",
    };

    private static MasterVerification GreenWith(params AcceptanceDisposition[] dispositions) =>
        new(VerificationStatus.Green, true, true, true, true, "ok", AcceptanceDispositions: dispositions);

    [Fact]
    public void Keystone_AllCriteriaMetOrJustified_Succeeds()
    {
        var v = GreenWith(
            new("All MediatR usages replaced by Mediator", AcceptanceStatus.Met, "swapped IMediator in DI"),
            new("All MassTransit usages replaced by Wolverine", AcceptanceStatus.NotApplicable,
                "no MassTransit present in this solution — nothing to migrate, no messaging behaviour changes"));

        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: v,
            ratifiedCriteria: TwoCriteria);

        verdict.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Keystone_CriterionUnmetActionable_Fails()
    {
        var v = GreenWith(
            new("All MediatR usages replaced by Mediator", AcceptanceStatus.Met, "done"),
            new("All MassTransit usages replaced by Wolverine", AcceptanceStatus.Unmet, ""));

        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: v,
            ratifiedCriteria: TwoCriteria);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("Wolverine");
        verdict.FailureReason.Should().Contain("acceptance criteria");
    }

    [Fact]
    public void Keystone_NotApplicableWithoutReason_Fails()
    {
        var v = GreenWith(
            new("All MediatR usages replaced by Mediator", AcceptanceStatus.Met, "done"),
            new("All MassTransit usages replaced by Wolverine", AcceptanceStatus.NotApplicable, "   "));

        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: v,
            ratifiedCriteria: TwoCriteria);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("without the required evaluated reason");
    }

    [Fact]
    public void Keystone_CriteriaButNoDispositions_Fails()
    {
        // The exact 2026-07-14 hole: a 1-file change + a green self-report, but the
        // master emitted no per-criterion disposition — the contract is unconfirmed.
        var green = new MasterVerification(VerificationStatus.Green, true, true, true, true, "ok");

        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: green,
            ratifiedCriteria: TwoCriteria);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("disposition");
    }

    [Fact]
    public void Keystone_NoRatifiedExpectation_FallsBackToChangeAndGreen()
    {
        // fix-bug graceful degrade: no contract → the change+green gate rules, so a
        // green run with a real change still succeeds without any dispositions.
        var green = new MasterVerification(VerificationStatus.Green, true, true, true, true, "ok");

        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: green,
            ratifiedCriteria: Array.Empty<string>());

        verdict.Satisfied.Should().BeTrue();
    }
}
