using System.Globalization;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-08-e8b9g: whether one more upload may join a conversation — under its byte cap, and
/// not a copy of one it already holds. A set is a copy when its sorted (path, hash) pairs equal a
/// stored set's; an image when its hash equals a stored image's; a set and an image are never
/// compared. A row stored before the hash column carries none and is never matched.
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
        var incoming = files.Select(f => (f.Path, (string?)f.Content.Sha256Hex()))
            .OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
        var copy = (await usage.FingerprintsAsync(sessionId, ReferenceFileKind.Site, ct))
            .FirstOrDefault(stored => stored.Files.All(f => f.Sha256 is not null) && stored.Files.SequenceEqual(incoming));
        return copy is null
            ? null
            : UploadRefusal.Duplicate($"This upload is already in the conversation as "
                + $"'{ReferenceSetName.Of(copy.Files.Select(f => f.Path))}', uploaded {When(copy.At)}.");
    }

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
