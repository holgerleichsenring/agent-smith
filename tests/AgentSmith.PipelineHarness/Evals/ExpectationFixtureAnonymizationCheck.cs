using System.Text.RegularExpressions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: the anonymization gate every fixture passes TWICE — at ingestion
/// (<see cref="ExpectationFixtureIngestion"/> refuses to write) and at load
/// (<see cref="ExpectationFixtureLoader"/> refuses to run). Two layers:
/// generic fingerprint patterns baked in CODE (emails, non-placeholder URLs,
/// hosting-org paths — never specific customer names, per repo policy), plus
/// an EXTENSIBLE deny-list file (<c>deny-patterns.txt</c>, one regex per
/// line, '#' comments) the operator grows with their own fingerprints. The
/// checks run over the raw fixture text so a fingerprint anywhere — title,
/// gold assertion, code map — is caught.
/// </summary>
public static class ExpectationFixtureAnonymizationCheck
{
    public const string DenyListFileName = "deny-patterns.txt";

    // Placeholder hosts that are legitimate inside an anonymized fixture.
    private static readonly Regex AllowedHost = new(
        @"^(localhost|127\.0\.0\.1|(.+\.)?example\.(com|org|net))$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly (Regex Pattern, string Reason)[] GenericChecks =
    [
        (new Regex(@"[A-Za-z0-9._%+-]+@([A-Za-z0-9.-]+\.[A-Za-z]{2,})",
            RegexOptions.Compiled), "email address"),
        (new Regex(@"https?://([^/\s""'\\]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "URL"),
        (new Regex(@"(dev\.azure\.com|visualstudio\.com|atlassian\.net|github\.com|" +
            @"gitlab\.com|bitbucket\.org|azurewebsites\.net)[/.][^\s""'\\]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), "hosting-org path"),
    ];

    /// <summary>Returns every violation found; an empty list means the fixture
    /// may be ingested/loaded. <paramref name="denyListDirectory"/> is scanned
    /// for the extensible deny-list file when non-null.</summary>
    public static IReadOnlyList<string> Check(
        ExpectationFixture fixture, string rawJson, string? denyListDirectory)
    {
        var violations = new List<string>();
        CheckAttestation(fixture, violations);
        foreach (var (pattern, reason) in GenericChecks)
            CheckPattern(rawJson, pattern, reason, violations);
        foreach (var deny in LoadDenyPatterns(denyListDirectory))
            CheckPattern(rawJson, deny, $"deny-list pattern '{deny}'", violations);
        return violations;
    }

    private static void CheckAttestation(ExpectationFixture fixture, List<string> violations)
    {
        if (fixture.Anonymization is not { Attested: true })
            violations.Add("missing anonymization attestation — the fixture must carry "
                + "'anonymization': { 'attested': true, 'by': '<who anonymized it>' }.");
        else if (string.IsNullOrWhiteSpace(fixture.Anonymization.By))
            violations.Add("anonymization attestation carries no 'by' — name who anonymized it.");
    }

    private static void CheckPattern(
        string rawJson, Regex pattern, string reason, List<string> violations)
    {
        foreach (Match match in pattern.Matches(rawJson))
        {
            if (IsAllowedPlaceholder(match)) continue;
            violations.Add($"customer-fingerprint suspect ({reason}): \"{Excerpt(match.Value)}\"");
        }
    }

    // Emails/URLs whose host is a documented placeholder stay legal — an
    // anonymized fixture may say user@example.com or https://api.example.com.
    private static bool IsAllowedPlaceholder(Match match) =>
        match.Groups.Count > 1
        && match.Groups[1].Success
        && AllowedHost.IsMatch(match.Groups[1].Value.Split(':')[0]);

    private static IEnumerable<Regex> LoadDenyPatterns(string? directory)
    {
        if (directory is null) yield break;
        var path = Path.Combine(directory, DenyListFileName);
        if (!File.Exists(path)) yield break;
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            yield return new Regex(trimmed, RegexOptions.IgnoreCase);
        }
    }

    private static string Excerpt(string value) =>
        value.Length <= 60 ? value : value[..60] + "…";
}
