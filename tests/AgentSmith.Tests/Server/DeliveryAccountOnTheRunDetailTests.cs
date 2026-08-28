using AgentSmith.Application.Services;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-25-7f5a: the run detail shows the account the GATE decided on.
/// <para>
/// The snapshot was built only from a negotiated expectation and the master's own
/// dispositions, while the gate has refused runs on the phase spec's criteria since p0393a.
/// A live run showed both at once: the failure named three ratified criteria and the card
/// said "No ratified acceptance contract on this run yet". Nothing was missing from the run —
/// the page was reading the wrong one of two judges.
/// </para>
/// </summary>
public sealed class DeliveryAccountOnTheRunDetailTests
{
    private static RunAccounts Accounts(params CriterionAccount[] criteria) =>
        RunAccounts.Empty.With("p1", [new SpecAccount("Sample.Server", criteria)]);

    private static RatifiedExpectation Ratified(string criterion) =>
        new(new ExpectationDraft("summary", [criterion], [], null),
            ExpectationOutcomes.Verbatim, "someone", DateTimeOffset.UnixEpoch, 0);

    private static AcceptanceView Deserialize(string? json) =>
        RunStoryJson.TryDeserialize<AcceptanceView>(json)
        ?? throw new InvalidOperationException("the snapshot did not round-trip");

    [Fact]
    public void RunStory_ADeliveryAccountExists_IsSnapshottedOverTheMasterDispositions()
    {
        var expectation = Ratified("the master's criterion");

        var view = Deserialize(RunStorySnapshotBuilder.BuildAcceptanceJson(
            expectation, verification: null,
            Accounts(new CriterionAccount("the gate's criterion", AccountDisposition.Satisfied, "src/A.cs", "found it"))));

        view.Criteria.Should().ContainSingle()
            .Which.Text.Should().Be("the gate's criterion",
                "the account is what refused the run, so it is what the page must show");
        view.Source.Should().Be(AcceptanceSources.DeliveryAccount);
    }

    [Fact]
    public void RunStory_NoDeliveryAccount_StillSnapshotsTheMasterDispositions()
    {
        var expectation = Ratified("the master's criterion");

        var view = Deserialize(RunStorySnapshotBuilder.BuildAcceptanceJson(
            expectation, verification: null, RunAccounts.Empty));

        view.Criteria.Should().ContainSingle().Which.Text.Should().Be("the master's criterion");
        view.Source.Should().Be(AcceptanceSources.MasterVerification,
            "most of the history has no phase account and must keep showing what it showed");
    }

    /// <summary>
    /// The run this phase exists for: criteria, a verdict, and no negotiated expectation
    /// anywhere. It used to snapshot nothing at all, so the handler published nothing and the
    /// page had an honest null to render as "no contract".
    /// </summary>
    [Fact]
    public void RunStory_AnAccountAndNoExpectation_StillProducesASnapshotToPublish()
    {
        var json = RunStorySnapshotBuilder.BuildAcceptanceJson(
            expectation: null, verification: null,
            Accounts(new CriterionAccount("a ratified criterion", AccountDisposition.NotSatisfied, null, "no file shows it")));

        json.Should().NotBeNull("the handler publishes when either payload exists");
        Deserialize(json).Criteria.Should().ContainSingle()
            .Which.Status.Should().Be(AcceptanceCriterionStatuses.Unmet);
    }

    [Fact]
    public void RunStory_NeitherAnAccountNorAnExpectation_SnapshotsNothing() =>
        RunStorySnapshotBuilder.BuildAcceptanceJson(null, null, RunAccounts.Empty)
            .Should().BeNull("a run with nothing to say serves an honest null");

    [Fact]
    public void RunStory_TheAccountSnapshot_CarriesItsCitation()
    {
        var view = Deserialize(RunStorySnapshotBuilder.BuildAcceptanceJson(
            null, null,
            Accounts(new CriterionAccount("a criterion", AccountDisposition.Satisfied, "src/Messaging/Installer.cs", "found"))));

        view.Criteria.Single().Citation.Should().Be("src/Messaging/Installer.cs",
            "a verdict a reader cannot check in twenty seconds is a verdict nobody checks");
        view.Criteria.Single().Reason.Should().Be("found");
    }

    /// <summary>
    /// An account that could not be TAKEN and a criterion that failed are different facts. A
    /// red build leaves the first, and reporting it as unmet would blame the delivery for a
    /// question nobody asked.
    /// </summary>
    [Fact]
    public void RunStory_AnAccountThatCouldNotBeTaken_IsUnprovenAndSaysWhy()
    {
        var view = Deserialize(RunStorySnapshotBuilder.BuildAcceptanceJson(
            null, null,
            RunAccounts.Empty.With("p1",
                [new SpecAccount("Sample.Server", [], "the build exited 1")])));

        var criterion = view.Criteria.Should().ContainSingle().Subject;
        criterion.Status.Should().Be(AcceptanceCriterionStatuses.Unproven);
        criterion.Reason.Should().Be("the build exited 1");
    }
}
