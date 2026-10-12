using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>What a derivation that yields nothing keeps instead of a fresh fallback, and the
/// shape a question hand-back must have — both split out when 3a36b and f5f7a were integrated.</summary>
public sealed class DerivationFailureKeepTests
{
    private static readonly SpecSet StoredHandback = new(
        "T-1", [], SpecAccounting.Empty, [], SpecSource.BranchArtifact,
        new SpecHandback(SpecHandbackCase.RequirementsContradictRepository, "the ticket names a file the repo lacks"));

    [Fact]
    public void KeptOnFailure_ExpiredHandback_IsKept() =>
        new SpecSourceResolver.Decision(SpecSource.BranchArtifact, StoredHandback, true, Expired: true)
            .KeptOnFailure.Should().BeSameAs(StoredHandback);

    [Fact]
    public void KeptOnFailure_NoExecutedHeadAndNotExpired_KeepsNothing() =>
        new SpecSourceResolver.Decision(SpecSource.BranchArtifact, StoredHandback, true)
            .KeptOnFailure.Should().BeNull();

    [Fact]
    public void KeptOnFailure_NoSet_KeepsNothing() =>
        new SpecSourceResolver.Decision(SpecSource.Derived, null, true, Expired: true)
            .KeptOnFailure.Should().BeNull();

    [Fact]
    public void QuestionShape_OneReading_IsRefused() =>
        QuestionHandbackShape.Rejection(["only one"], 0).Should().Contain("at least 2 readings");

    [Fact]
    public void QuestionShape_TakenOutsideReadings_IsRefused() =>
        QuestionHandbackShape.Rejection(["a", "b"], 2).Should().Contain("names none of the 2 readings");

    [Fact]
    public void QuestionShape_TwoReadingsOneTaken_IsAQuestion() =>
        QuestionHandbackShape.Rejection(["a", "b"], 1).Should().BeNull();
}
