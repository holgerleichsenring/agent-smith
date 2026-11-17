using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// 2026-11-17-166ae: one worker call's prompt in its two parts — <see cref="System"/>, the same on every call of a
/// conversation, and <see cref="Input"/>, what changes. A binary that takes no system prompt reads <see cref="Text"/>.
/// </summary>
public sealed record WorkerPrompt(string System, string Input)
{
    /// <summary>Both parts as one text, system part first.</summary>
    public string Text => System + Input;

    /// <summary>Characters of both parts — the size a failure names.</summary>
    public int Length => System.Length + Input.Length;

    /// <summary>
    /// 2026-11-17-d6f5: the input part as content blocks — the call's identity, then one block per message. A cache
    /// entry is read only where a block ends, so the previous call's blocks, unchanged, are the next call's cache hit.
    /// </summary>
    public IReadOnlyList<string> InputBlocks { get; init; } = [Input];

    /// <summary>
    /// 2026-11-17-d6f5: the same id on every call of one conversation — a UUID from the system part and the first two
    /// blocks (identity and opening message). The CLI writes its session id into its own prompt, ahead of the input.
    /// </summary>
    public string ConversationId =>
        Uuid(SHA256.HashData(Encoding.UTF8.GetBytes(System + string.Concat(InputBlocks.Take(2))))); // not a limit: identity and opening message

    // A version-5-shaped UUID, so a CLI that validates the id accepts it.
    private static string Uuid(byte[] hash)
    {
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var hex = Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}
