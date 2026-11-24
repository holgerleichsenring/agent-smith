using System.Text.Json;

namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dc: what a PoC scan decided — the findings that block and the notes. Decided only by the block rules;
/// an explanation never changes it. It is <see cref="Overrulable"/> when every block is a vulnerability: a blocking
/// secret can never be overruled.
/// </summary>
public sealed record PocScanVerdict(IReadOnlyList<PocScanFinding> Blocks, IReadOnlyList<PocScanFinding> Notes)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool IsBlocked => Blocks.Count > 0;

    public bool Overrulable => IsBlocked && Blocks.All(b => b.Kind == PocScanFindingKinds.Vulnerability);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static PocScanVerdict? FromJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PocScanVerdict>(json, Json);
}
