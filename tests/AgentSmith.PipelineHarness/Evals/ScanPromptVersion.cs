using System.Security.Cryptography;
using System.Text;
using AgentSmith.Tests.Prompts;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: what a scan report is keyed by on the prompt side — a short digest of
/// the scan master's own body, as the PINNED catalog ships it.
/// <para>
/// Derived rather than declared, the reason <see cref="AccountPromptVersion"/> gives: a
/// hand-maintained version number is a number somebody forgets to raise, and the report
/// would then overwrite a baseline taken under different instructions. Digesting the body
/// means the key moves exactly when the thing under test moves, and never otherwise —
/// including when the only change is a catalog pin bump.
/// </para>
/// </summary>
public static class ScanPromptVersion
{
    public const string SecurityMaster = "security-master";
    public const string ApiSecurityMaster = "api-security-master";

    /// <summary>The digest for one master's body. Eight hex characters: enough that two
    /// different prompts do not collide in one report directory, short enough to read in a
    /// file name.</summary>
    public static string For(string masterSkillName) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(PackagedMaster.Read(masterSkillName))))[..8]
            .ToLowerInvariant();
}
