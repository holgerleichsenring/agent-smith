using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-11-10-8e59: a review that ended clean — its last taken round said build — on exactly this text: the spec id
/// and the SHA-256 of the yaml it attacked. Any changed byte makes it not apply, and the text is attacked again
/// before it is built.
/// </summary>
public sealed record ReviewedText(string SpecId, string Sha256)
{
    public static ReviewedText Of(string specId, string yaml) => new(specId, Hash(yaml));

    public static string Hash(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(yaml)));
    }

    /// <summary>Whether <paramref name="reviewed"/> holds a clean review of exactly this text under this id.</summary>
    public static bool Covers(IReadOnlyList<ReviewedText>? reviewed, string specId, string yaml) =>
        reviewed?.Contains(Of(specId, yaml)) == true;
}
