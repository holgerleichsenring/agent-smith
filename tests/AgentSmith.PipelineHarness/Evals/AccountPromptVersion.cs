using System.Security.Cryptography;
using System.Text;
using AgentSmith.Application.Services.Specs;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-25-7035: what the report is keyed by on the prompt side — a short digest of the
/// account's own instructions.
/// <para>
/// Derived rather than declared, because a hand-maintained version number is a number
/// somebody forgets to raise, and the report would then overwrite a baseline taken under
/// different instructions. Digesting the rendered prompt means the key moves exactly when
/// the thing under test moves, and never otherwise.
/// </para>
/// </summary>
public static class AccountPromptVersion
{
    /// <summary>The digest of the instructions, with the criteria, diff and command list held
    /// constant so only the WORDING contributes.</summary>
    public static string Current { get; } = Digest(
        SpecAccountPrompt.For(["a criterion"], string.Empty, [], ["Sample.Server"]));

    private static string Digest(string prompt) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prompt)))[..8].ToLowerInvariant();
}
