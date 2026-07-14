using System.Text.Json;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: the operator's ingestion helper for turning a real historical
/// ticket into a replay golden. Enforcement, not convenience: the composed
/// fixture is serialized, run through the FULL anonymization + shape gate,
/// and REFUSED (nothing written) on any violation — identifying material can
/// never be committed via this path. The seed set itself must come from the
/// operator (real tickets with human-authored acceptance criteria cannot be
/// invented); this helper is how each one enters the fixture directory.
/// </summary>
public static class ExpectationFixtureIngestion
{
    private static readonly JsonSerializerOptions WriteJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    /// <summary>Validates and writes <paramref name="fixture"/> into
    /// <paramref name="targetDirectory"/> as <c>{id}.json</c>. Throws
    /// <see cref="InvalidDataException"/> listing every violation when the
    /// fixture is malformed or carries customer-fingerprint material; the
    /// deny-list in the target directory participates in the check.</summary>
    public static string Ingest(ExpectationFixture fixture, string targetDirectory)
    {
        var rawJson = JsonSerializer.Serialize(fixture, WriteJson);
        var errors = new List<string>();
        errors.AddRange(ExpectationFixtureLoader.ValidateShape(fixture));
        errors.AddRange(ExpectationFixtureAnonymizationCheck.Check(
            fixture, rawJson, targetDirectory));
        if (errors.Count > 0)
            throw new InvalidDataException(
                $"Fixture '{fixture.Id}' refused at ingestion — anonymize and retry:\n- "
                + string.Join("\n- ", errors));
        Directory.CreateDirectory(targetDirectory);
        var path = Path.Combine(targetDirectory, $"{fixture.Id}.json");
        File.WriteAllText(path, rawJson);
        return path;
    }
}
