using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-11-10-8e59: a review that ended clean — its last taken round said build — on exactly this text: the spec id
/// (or <see cref="Models.ProposalReviewTokens.Series"/> for a series contract) and the SHA-256 of the text it attacked.
/// Any changed byte makes it not apply, and the text is attacked again before it is built.
/// </summary>
public sealed record ReviewedText(string SpecId, string Sha256)
{
    public static ReviewedText Of(string specId, string yaml) => new(specId, Hash(yaml));

    /// <summary>The series contract's clean review, under the series token.</summary>
    public static ReviewedText Of(SeriesContract contract) => new(Models.ProposalReviewTokens.Series, Hash(contract));

    /// <summary>The hash of a spec's yaml in its canonical form: trimmed, as the branch write and the reader both
    /// agree on — a filed file ends in one newline and the reader trims, so the ends are not the text.</summary>
    public static string Hash(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Sha(yaml.Trim());
    }

    /// <summary>The hash of a series contract: its JSON, which a manifest's yaml round trip reproduces.</summary>
    public static string Hash(SeriesContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return Sha(JsonSerializer.Serialize(contract));
    }

    /// <summary>Whether <paramref name="reviewed"/> holds a clean review of exactly this text under this id.</summary>
    public static bool Covers(IReadOnlyList<ReviewedText>? reviewed, string specId, string yaml) =>
        reviewed?.Contains(Of(specId, yaml)) == true;

    /// <summary>Whether <paramref name="reviewed"/> holds a clean review of exactly this contract.</summary>
    public static bool Covers(IReadOnlyList<ReviewedText>? reviewed, SeriesContract contract) =>
        reviewed?.Contains(Of(contract)) == true;

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
