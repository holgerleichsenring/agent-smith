namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-17-f3a8d: one installation secret value, envelope-encrypted: the value under a data key of its own
/// (AES-GCM, random nonce, the tag appended), the data key wrapped under the master key <see cref="KeyId"/> names.
/// The master key is never in this database. 2026-11-23-933dg: the key is (<see cref="Scope"/>, <see cref="Name"/>).
/// </summary>
public sealed class StoredSecret : EntityBase
{
    /// <summary>2026-11-23-933dg: 'installation' (a catalog secret) or 'poc:&lt;id&gt;' (a PoC variable).</summary>
    public string Scope { get; set; } = AgentSmith.Contracts.Models.SecretScope.InstallationText;

    /// <summary>An installation secret's name in lower case; a PoC variable's name as written (upper case by rule).
    /// Part of the key and of both associated data.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The envelope format; 1 is AES-256-GCM for value and key.</summary>
    public int Version { get; set; }

    public byte[] Ciphertext { get; set; } = [];

    public byte[] Nonce { get; set; } = [];

    public byte[] WrappedKey { get; set; } = [];

    public byte[] WrapNonce { get; set; } = [];

    public string KeyId { get; set; } = string.Empty;

    public string SetBy { get; set; } = string.Empty;

    public DateTimeOffset SetAt { get; set; }
}
