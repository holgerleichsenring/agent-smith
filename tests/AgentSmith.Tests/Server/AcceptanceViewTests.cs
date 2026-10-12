using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-12-6bfad: an account nobody could take is listed apart from the criteria, and a
/// snapshot archived before that distinction is read into the same shape.
/// </summary>
public sealed class AcceptanceViewTests
{
    [Fact]
    public void AcceptanceView_ArchivedDeliveryAccountRow_IsLiftedToUnmeasured()
    {
        var archived = RunStoryJson.Serialize(new
        {
            criteria = new[]
            {
                new { text = "the endpoint returns 200", status = "met", reason = (string?)"found" },
                new { text = "default", status = "unproven", reason = (string?)"sbt: not found" },
            },
            outcome = "verbatim",
            ratifiedBy = "the ratified spec",
            source = AcceptanceSources.DeliveryAccount,
        });

        var view = RunStoryJson.TryDeserialize<AcceptanceView>(archived)!;

        view.Criteria.Should().ContainSingle().Which.Text.Should().Be("the endpoint returns 200");
        view.Unmeasured.Should().Equal(new UnmeasuredAccountView("default", "sbt: not found"));
    }

    [Fact]
    public void AcceptanceView_MasterVerificationUnproven_StaysACriterion()
    {
        var view = new AcceptanceView(
            [new AcceptanceCriterionView("a third criterion", AcceptanceCriterionStatuses.Unproven, null)],
            "verbatim", "operator", AcceptanceSources.MasterVerification);

        view.Criteria.Should().ContainSingle("the master reporting nothing is a real criterion left open");
        view.Unmeasured.Should().BeEmpty();
    }

    [Fact]
    public void AcceptanceView_NewShape_RoundTripsUnchanged()
    {
        var written = new AcceptanceView(
            [new AcceptanceCriterionView("c", AcceptanceCriterionStatuses.Unmet, "missing")],
            "verbatim", "spec", AcceptanceSources.DeliveryAccount,
            Unmeasured: [new UnmeasuredAccountView("a, b", "the delivery diff could not be taken")]);

        var json = RunStoryJson.Serialize(written);
        var read = RunStoryJson.TryDeserialize<AcceptanceView>(json)!;

        json.Should().Contain("\"unmeasured\":[{\"repositories\":\"a, b\"");
        read.Criteria.Should().Equal(written.Criteria);
        read.Unmeasured.Should().Equal(written.Unmeasured);
    }
}
