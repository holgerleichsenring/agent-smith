using System.Text.Json;
using AgentSmith.Application.Services.Expectations;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: loads the replay goldens and refuses anything malformed OR
/// unanonymized — the load path enforces the same gate as ingestion, so a
/// fixture hand-copied past <see cref="ExpectationFixtureIngestion"/> still
/// cannot reach an eval run. Shape validation reuses the production
/// <see cref="ExpectationDraftValidator"/> on the gold block: a gold standard
/// that violates the p0328 caps would make every draft look wrong.
/// </summary>
public static class ExpectationFixtureLoader
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static IReadOnlyList<ExpectationFixture> LoadAll(string directory)
    {
        if (!Directory.Exists(directory))
            throw new InvalidDataException($"Fixture directory not found: {directory}");
        return Directory.GetFiles(directory, "*.json").Order()
            .Select(Load)
            .ToList();
    }

    public static ExpectationFixture Load(string path)
    {
        var rawJson = File.ReadAllText(path);
        var fixture = Deserialize(rawJson, path);
        var errors = new List<string>();
        errors.AddRange(ValidateShape(fixture));
        errors.AddRange(ExpectationFixtureAnonymizationCheck.Check(
            fixture, rawJson, Path.GetDirectoryName(path)));
        if (errors.Count > 0)
            throw new InvalidDataException(
                $"Fixture '{Path.GetFileName(path)}' rejected:\n- {string.Join("\n- ", errors)}");
        return fixture with
        {
            Gold = fixture.Gold! with { Constraints = fixture.Gold!.Constraints ?? [] },
        };
    }

    internal static IReadOnlyList<string> ValidateShape(ExpectationFixture fixture)
    {
        var errors = new List<string>();
        if (fixture.Version != ExpectationFixture.CurrentVersion)
            errors.Add($"'version' must be {ExpectationFixture.CurrentVersion} (got {fixture.Version}).");
        if (string.IsNullOrWhiteSpace(fixture.Id))
            errors.Add("'id' must not be empty.");
        if (string.IsNullOrWhiteSpace(fixture.Ticket?.Title)
            || string.IsNullOrWhiteSpace(fixture.Ticket?.Description))
            errors.Add("'ticket' must carry a title and a description.");
        ValidateGold(fixture, errors);
        return errors;
    }

    private static void ValidateGold(ExpectationFixture fixture, List<string> errors)
    {
        if (fixture.Gold is null)
        {
            errors.Add("'gold' (the human-authored expectation) is required.");
            return;
        }
        // STJ leaves omitted arrays null on the positional record — normalize
        // so the production validator (which assumes non-null lists) applies.
        if (fixture.Gold.Expected is null)
        {
            errors.Add("'gold' must carry an 'expected' array.");
            return;
        }
        errors.AddRange(new ExpectationDraftValidator()
            .Validate(fixture.Gold with { Constraints = fixture.Gold.Constraints ?? [] })
            .Select(e => $"gold expectation violates the p0328 schema caps: {e}"));
    }

    private static ExpectationFixture Deserialize(string rawJson, string path)
    {
        try
        {
            return JsonSerializer.Deserialize<ExpectationFixture>(rawJson, Json)
                ?? throw new InvalidDataException($"Fixture '{path}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Fixture '{path}' is not valid JSON: {ex.Message}");
        }
    }
}
