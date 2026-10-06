using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-02-3f06a: the premise keys every producer already writes — facts, assumptions,
/// applies_to, contexts — have a declared shape. The top level stays open, so before this a
/// fact without evidence or with a stray key passed unchecked into the run.
/// </summary>
public sealed class PhaseSpecPremiseKeysSchemaTests
{
    private const string Head = "spec: 2026-10-02-0000\ngoal: \"g\"\n";

    [Fact]
    public void PhaseSpecSchema_FactWithoutEvidence_IsInvalid() =>
        Errors("facts:\n  - claim: \"a claim\"\n").Should().NotBeEmpty();

    [Fact]
    public void PhaseSpecSchema_FactWithAnExtraKey_IsInvalid() =>
        Errors("facts:\n  - claim: \"a claim\"\n    evidence: \"a.cs:1\"\n    source: \"x\"\n")
            .Should().NotBeEmpty("a fact is exactly a claim and its evidence");

    [Fact]
    public void PhaseSpecSchema_AssumptionAsClaimAndCheck_IsValid() =>
        Errors("assumptions:\n  - claim: \"a premise\"\n    check: \"how to test it\"\n  - \"a bare one\"\n")
            .Should().BeEmpty();

    [Theory]
    [InlineData("facts: []\nassumptions: []\n")]
    [InlineData("facts:\nassumptions:\n")]
    [InlineData("applies_to: \"specs\"\ncontexts: [default]\n")]
    [InlineData("contexts: default\nassumptions: \"one premise\"\n")]
    public void PhaseSpecSchema_EmptyAndNullFactsAndAssumptions_AreValid(string premises) =>
        Errors(premises).Should().BeEmpty("an empty list is a statement and null says nothing");

    private static IReadOnlyList<string> Errors(string premises) =>
        PhaseSpecSchemaFile.Validate(YamlAsJson.Convert(Head + premises)!);
}
