using FluentAssertions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0397: fast-tier anonymization gate for the plan-golden fixture directory —
/// the same fingerprint patterns the expectation goldens pass at load, applied
/// to the raw markdown of every committed plan ticket fixture. Real tickets
/// are NEVER committed here (they ride in via AGENTSMITH_EVAL_TICKET_FILE);
/// this pins that the committed synthetic ones stay clean.
/// </summary>
public sealed class PlanGoldenFixtureAnonymizationTests
{
    [Fact]
    public void EveryCommittedPlanGoldenFixture_PassesTheAnonymizationCheck()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanGoldens");
        var fixtures = Directory.GetFiles(directory, "*.md");
        fixtures.Should().NotBeEmpty("the synthetic two-repo migration fixture is committed");

        foreach (var path in fixtures)
        {
            // The check's fixture argument only feeds the attestation rule;
            // markdown fixtures attest via this committed test instead of
            // JSON frontmatter, so a pre-attested stub satisfies it and the
            // pattern checks run over the raw fixture text.
            var attested = new ExpectationFixture(
                ExpectationFixture.CurrentVersion,
                Path.GetFileNameWithoutExtension(path),
                Synthetic: true,
                new ExpectationFixture.Attestation(true, "p0397 synthetic fixture author", "2026-08-04"),
                Ticket: null, ContextHints: null, Gold: null);

            var violations = ExpectationFixtureAnonymizationCheck.Check(
                attested, File.ReadAllText(path), directory);

            violations.Should().BeEmpty(
                "plan-golden fixture {0} must stay anonymized", Path.GetFileName(path));
        }
    }
}
