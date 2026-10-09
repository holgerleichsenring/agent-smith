using System.Globalization;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-08-e8b9g: whether one more upload may join a conversation — under its byte cap, and
/// not a copy of what it already holds. An image is a copy when its hash equals a stored image's;
/// a set and an image are never compared. A row stored before the hash column carries none and is
/// never matched.
/// 2026-10-09-86e1: a set is a copy when EVERY one of its files' content is held by some set of
/// the conversation, path ignored. A pick with any new file is stored WHOLE — a second version of
/// a folder must stay complete — and the page marks the held files before sending.
/// <para>
/// NEITHER CHECK TAKES A LOCK. Two uploads at once may both pass: the cap is overshot by at most
/// one upload, and a pair may both be stored. Neither costs more than the upload itself.
/// </para>
/// </summary>
public sealed class ConversationUploadAdmission(ReferenceUsageRepository usage)
{
    /// <summary>Null when the set may be stored; otherwise the refusal to answer.</summary>
    public async Task<UploadRefusal?> RefusalForSetAsync(
        string sessionId, IReadOnlyList<(string Path, byte[] Content)> files, CancellationToken ct)
    {
        if (await OverCapAsync(sessionId, files.Sum(f => f.Content.LongLength), ct) is { } over) return over;
        var holders = HeldIn(await usage.FingerprintsAsync(sessionId, ReferenceFileKind.Site, ct));
        var held = files.Select(f => holders.GetValueOrDefault(f.Content.Sha256Hex())).ToList();
        if (held.Any(h => h is null)) return null;
        var sets = held.OfType<StoredUploadFingerprint>().DistinctBy(h => h.SetId).ToList();
        return UploadRefusal.Duplicate(sets is [var only]
            ? $"This upload is already in the conversation as '{NameOf(only)}', uploaded {When(only.At)}."
            : "Every file of this upload is already in the conversation, in "
                + string.Join(", ", sets.Select(s => $"'{NameOf(s)}'")) + ".");
    }

    /// <summary>2026-10-09-86e1: every content hash the conversation's sets hold, with the oldest set holding it.</summary>
    public async Task<IReadOnlyList<HeldContentView>> HeldAsync(string sessionId, CancellationToken ct) =>
        [.. HeldIn(await usage.FingerprintsAsync(sessionId, ReferenceFileKind.Site, ct))
            .Select(h => new HeldContentView(h.Key, h.Value.SetId, NameOf(h.Value)))];

    /// <summary>Each held content hash with the oldest set holding it.</summary>
    private static Dictionary<string, StoredUploadFingerprint> HeldIn(IReadOnlyList<StoredUploadFingerprint> stored)
    {
        var held = new Dictionary<string, StoredUploadFingerprint>(StringComparer.Ordinal);
        foreach (var set in stored.OrderBy(s => s.At))
            foreach (var hash in set.Files.Select(f => f.Sha256).OfType<string>()) held.TryAdd(hash, set);
        return held;
    }

    private static string NameOf(StoredUploadFingerprint set) => ReferenceSetName.Of(set.Files.Select(f => f.Path));

    /// <summary>Null when the image may be stored; otherwise the refusal to answer.</summary>
    public async Task<UploadRefusal?> RefusalForImageAsync(string sessionId, byte[] content, CancellationToken ct)
    {
        if (await OverCapAsync(sessionId, content.LongLength, ct) is { } over) return over;
        var hash = content.Sha256Hex();
        var copy = (await usage.FingerprintsAsync(sessionId, ReferenceFileKind.Image, ct))
            .FirstOrDefault(stored => stored.Files.Any(f => f.Sha256 == hash));
        return copy is null
            ? null
            : UploadRefusal.Duplicate($"This image is already in the conversation, attached {When(copy.At)}.");
    }

    private async Task<UploadRefusal?> OverCapAsync(string sessionId, long incoming, CancellationToken ct)
    {
        var used = await usage.BytesAsync(sessionId, ct);
        if (used + incoming <= ReferenceUploadLimits.MaxConversationBytes) return null;
        return UploadRefusal.OverCap($"This conversation holds {ReferenceUploadLimits.Megabytes(used)} of uploads and "
            + $"this one adds {ReferenceUploadLimits.Megabytes(incoming)}, over the "
            + $"{ReferenceUploadLimits.Megabytes(ReferenceUploadLimits.MaxConversationBytes)} a conversation may hold. "
            + "Remove an upload to make room.");
    }

    private static string When(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
