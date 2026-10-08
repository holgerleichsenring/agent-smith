using System.Security.Cryptography;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// 2026-10-08-e8b9g: the one way an uploaded file's content hash is written, so the upload that
/// stores a row and the check that compares against it cannot spell it two ways.
/// </summary>
public static class ReferenceContentHashExtensions
{
    /// <summary>The SHA-256 of <paramref name="content"/> as lower-case hex.</summary>
    public static string Sha256Hex(this byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Convert.ToHexStringLower(SHA256.HashData(content));
    }
}
