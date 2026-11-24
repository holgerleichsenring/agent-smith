using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: the session key a person's PoC uploads are stored under in the ReferenceFile byte store —
/// 'poc-upload:' plus the first 16 hex of the sha256 of their principal. An upload is read back only with the caller's
/// own key, so another person's upload id finds nothing; reference deletions are keyed on a conversation's session id,
/// so none reaches it.
/// </summary>
internal static class PocUploadKey
{
    public const string Prefix = "poc-upload:";

    private const int OwnerHexChars = 16; // not a limit: 64 bits of the principal's hash keep owners apart

    public static string For(ClaimsPrincipal user) => Of(SpecDialogOwnership.PrincipalOf(user));

    public static string Of(string principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(principal)));
        return Prefix + hash[..OwnerHexChars];
    }
}
