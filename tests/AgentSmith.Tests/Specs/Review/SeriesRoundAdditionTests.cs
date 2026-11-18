using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-11-18-c8e2e: a series contract cut that adds to a member is never applied; it is a finding.</summary>
public sealed class SeriesRoundAdditionTests
{
    [Fact]
    public void Split_AnAddingMemberCut_IsAFindingNotApplied()
    {
        var member = new DerivedSpec(new SpecDraft("s1", "goal", "spec: s1", []), "s1", string.Empty, []);
        var set = new SpecSet("k", [member], SpecAccounting.Empty, [], SpecSource.Derived);
        var adding = new SpecReviewCut(SpecRegisterNames.Steps, "Document the order endpoints",
            "Document the order endpoints in an OpenAPI matrix", "shop/a:1 [A1]", "s1");

        var split = SeriesRoundScope.Split(new SpecAttackResult(SpecReviewVerdicts.BuildWithCuts, [adding], []), set, [member], ["s1"]);

        split.Landing.Cuts.Should().BeEmpty();
        split.Landing.Findings.Should().ContainSingle().Which.Text.Should().Contain("OpenAPI matrix");
        split.Unapplied.Should().Be(1, "an unapplied cut leaves the contract not reviewed clean, so the author is asked");
    }
}
