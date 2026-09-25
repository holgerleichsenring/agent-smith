using AgentSmith.Infrastructure.Persistence.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Maps between the durable <see cref="SpecDialogSession"/> entity and the
/// in-memory <see cref="ConversationState"/> shape (transcript + scope are
/// stored as JSON columns).
/// </summary>
internal static class SpecDialogSessionMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // 042ec, 042el: a kind or a decision this build cannot name reads as none, not as an unreadable transcript.
        Converters =
        {
            new TolerantNullableEnumConverter<SpecDialogTurnKind>(),
            new TolerantNullableEnumConverter<SpecDialogDecision>(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };

    internal static ConversationState ToState(SpecDialogSession session) => new()
    {
        JobId = session.SessionId,
        ChannelId = session.ChannelId,
        UserId = session.UserId,
        Platform = session.Platform,
        Project = session.Project,
        TicketId = string.Empty,
        StartedAt = session.CreatedAt,
        LastActivityAt = session.LastActivityAt,
        Mode = ConversationMode.SpecDialog,
        ThreadId = session.ThreadId,
        Transcript = ReadTranscript(session.TranscriptJson),
        Subject = session.Subject,
        Tracker = session.Tracker,
        TicketKey = session.TicketKey,
        Scope = new ActiveScope
        {
            Project = session.Project,
            Repos = ReadRepos(session.ReposJson),
        },
    };

    /// <summary>
    /// 2026-09-25-8e51b: a bound conversation's heading — the ticket's title, trimmed to the
    /// column. NOT put through the subject admission rule: that rule refuses brackets, quotes and
    /// leading dashes because it is judging a MODEL's one-line answer, which is minted once and
    /// never revised, and a real ticket title routinely carries all three. A title is not a guess.
    /// </summary>
    internal static string? Heading(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= PersistenceLimits.ConversationSubject
            ? trimmed
            : trimmed[..PersistenceLimits.ConversationSubject];
    }

    internal static string WriteTranscript(IReadOnlyList<TranscriptTurn> transcript) =>
        JsonSerializer.Serialize(transcript, JsonOptions);

    internal static IReadOnlyList<TranscriptTurn> ReadTranscript(string json) =>
        JsonSerializer.Deserialize<List<TranscriptTurn>>(json, JsonOptions) ?? [];

    internal static string WriteRepos(IReadOnlyList<string> repos) =>
        JsonSerializer.Serialize(repos, JsonOptions);

    internal static IReadOnlyList<string> ReadRepos(string json) =>
        JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
}
