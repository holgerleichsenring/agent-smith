using System.Text.Json;

namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dc: the finding ids overrules lifted, keyed by stage ({"code": [...], "image": [...]}). An overrule
/// APPENDS under its stage's key and never replaces another stage's or an earlier overrule's ids; the code re-scan reads
/// only 'code'.
/// </summary>
public sealed record PocOverruledIds(IReadOnlyDictionary<string, IReadOnlyList<string>> ByStage)
{
    public static PocOverruledIds Empty { get; } = new(new Dictionary<string, IReadOnlyList<string>>());

    public IReadOnlyList<string> Of(string stage) => ByStage.TryGetValue(stage, out var ids) ? ids : [];

    public PocOverruledIds Append(string stage, IEnumerable<string> ids)
    {
        var next = new Dictionary<string, IReadOnlyList<string>>(ByStage, StringComparer.Ordinal)
        {
            [stage] = [.. Of(stage).Concat(ids).Distinct(StringComparer.Ordinal)],
        };
        return new PocOverruledIds(next);
    }

    public string ToJson() => JsonSerializer.Serialize(ByStage);

    public static PocOverruledIds FromJson(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? Empty
            : new PocOverruledIds(JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json)!
                .ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal));
}
