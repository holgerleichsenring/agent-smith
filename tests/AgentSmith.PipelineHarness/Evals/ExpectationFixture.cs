using AgentSmith.Contracts.Expectations;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: one replay golden — an anonymized historical ticket plus the
/// human-authored expectation as gold standard. Versioned test asset under
/// <c>Fixtures/ExpectationGoldens/</c> (snake_case JSON). The gold block uses
/// the p0328 <see cref="ExpectationDraft"/> shape so the same validator caps
/// apply to the human reference as to the drafts measured against it.
/// The anonymization attestation is MANDATORY: the loader and the ingestion
/// helper both reject a fixture that does not carry an explicit attestation
/// (see <see cref="ExpectationFixtureAnonymizationCheck"/>).
/// </summary>
public sealed record ExpectationFixture(
    int Version,
    string Id,
    bool Synthetic,
    ExpectationFixture.Attestation? Anonymization,
    ExpectationFixture.TicketMaterial? Ticket,
    ExpectationFixture.Hints? ContextHints,
    ExpectationDraft? Gold)
{
    public const int CurrentVersion = 1;

    /// <summary>The fixture-local anonymization attestation: the operator who
    /// ingested the ticket asserts identifying material was removed.</summary>
    public sealed record Attestation(bool Attested, string? By, string? Date);

    /// <summary>The replayed ticket text — what the drafter sees.</summary>
    public sealed record TicketMaterial(
        string? Title, string? Description, string? AcceptanceCriteria);

    /// <summary>Optional analysis-stage context (the fix-bug preset drafts
    /// AFTER AnalyzeCode, so a realistic replay grounds the draft the same
    /// way).</summary>
    public sealed record Hints(string? CodeMap);
}
