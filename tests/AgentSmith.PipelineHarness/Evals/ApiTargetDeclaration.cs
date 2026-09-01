using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: what is wrong with the served target, per method and path template.
/// <para>
/// It sits beside the document rather than inside it. An OpenAPI extension would be text
/// the scan under test can read, and a corpus whose answers are printed on the thing being
/// scanned measures nothing.
/// </para>
/// </summary>
public sealed record ApiTargetDeclaration
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;

    [JsonPropertyName("endpoints")]
    public IReadOnlyList<ApiEndpointDeclaration> Endpoints { get; init; } = [];

    public IEnumerable<ApiEndpointDeclaration> Weak => Endpoints.Where(e => e.IsWeak);

    public IEnumerable<ApiEndpointDeclaration> Sound => Endpoints.Where(e => e.IsSound);

    public static class Verdicts
    {
        public const string Weak = "weak";
        public const string Sound = "sound";

        public static IReadOnlyList<string> All => [Weak, Sound];
    }
}

/// <summary>
/// One endpoint and the truth about it. At most ONE declaration per (method, path), so two
/// declarations cannot collapse into a single match.
/// </summary>
public sealed record ApiEndpointDeclaration
{
    [JsonPropertyName("method")] public string Method { get; init; } = string.Empty;

    /// <summary>The path TEMPLATE, as the document declares it — <c>/members/{id}</c>.</summary>
    [JsonPropertyName("path")] public string Path { get; init; } = string.Empty;

    /// <summary>One of <see cref="ApiTargetDeclaration.Verdicts"/>.</summary>
    [JsonPropertyName("verdict")] public string Verdict { get; init; } = string.Empty;

    /// <summary>The weakness class for a weak endpoint; for a sound one, the class it is
    /// shaped to be mistaken for.</summary>
    [JsonPropertyName("class")] public string Class { get; init; } = string.Empty;

    [JsonPropertyName("because")] public string Because { get; init; } = string.Empty;

    public bool IsWeak =>
        string.Equals(Verdict, ApiTargetDeclaration.Verdicts.Weak, StringComparison.Ordinal);

    public bool IsSound =>
        string.Equals(Verdict, ApiTargetDeclaration.Verdicts.Sound, StringComparison.Ordinal);

    public bool HasKnownVerdict => IsWeak || IsSound;

    /// <summary>How the endpoint is named in a report and in a match key.</summary>
    public string Describe() => $"{Method.ToUpperInvariant()} {Path}";
}

/// <summary>2026-09-01-6686: loads the declaration and refuses one that scores nothing.</summary>
public static class ApiTargetDeclarationLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>The fixture directory as the built harness ships it.</summary>
    public static string DefaultDirectory =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "StubApiTarget");

    public static string DefaultPath =>
        System.IO.Path.Combine(DefaultDirectory, "declaration.json");

    public static ApiTargetDeclaration Load(string path)
    {
        var raw = File.ReadAllText(path);
        var violations = ExpectationFixtureAnonymizationCheck.CheckText(raw, DefaultDirectory);
        if (violations.Count > 0)
            throw new InvalidOperationException(
                $"{System.IO.Path.GetFileName(path)} carries customer fingerprints:\n  "
                + string.Join("\n  ", violations));
        var declaration = JsonSerializer.Deserialize<ApiTargetDeclaration>(raw, Options)
            ?? throw new InvalidOperationException($"{path} is not an api-target declaration.");
        Validate(declaration, System.IO.Path.GetFileName(path));
        return declaration;
    }

    private static void Validate(ApiTargetDeclaration declaration, string fileName)
    {
        var unknown = declaration.Endpoints
            .Where(e => !e.HasKnownVerdict).Select(e => e.Describe()).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"{fileName} declares no verdict for: {string.Join(", ", unknown)}. "
                + $"Every endpoint is one of {string.Join(" / ", ApiTargetDeclaration.Verdicts.All)}.");
        var duplicates = declaration.Endpoints
            .GroupBy(e => e.Describe(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"{fileName} declares the same endpoint twice: {string.Join(", ", duplicates)}. "
                + "Scoring is per endpoint, so one endpoint is one verdict.");
    }
}
