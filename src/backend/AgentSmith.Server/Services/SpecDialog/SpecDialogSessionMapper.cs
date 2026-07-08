using System.Text.Json;
using System.Text.Json.Serialization;
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
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    internal static ConversationState ToState(SpecDialogSession session) => new()
    {
        JobId = session.SessionId,
        ChannelId = session.ChannelId,
        UserId = session.UserId,
        Platform = session.Platform,
        Project = session.Project,
        TicketId = 0,
        StartedAt = session.CreatedAt,
        LastActivityAt = session.LastActivityAt,
        Mode = ConversationMode.SpecDialog,
        ThreadId = session.ThreadId,
        Transcript = ReadTranscript(session.TranscriptJson),
        Scope = new ActiveScope
        {
            Project = session.Project,
            Repos = ReadRepos(session.ReposJson),
        },
    };

    internal static string WriteTranscript(IReadOnlyList<TranscriptTurn> transcript) =>
        JsonSerializer.Serialize(transcript, JsonOptions);

    internal static IReadOnlyList<TranscriptTurn> ReadTranscript(string json) =>
        JsonSerializer.Deserialize<List<TranscriptTurn>>(json, JsonOptions) ?? [];

    internal static string WriteRepos(IReadOnlyList<string> repos) =>
        JsonSerializer.Serialize(repos, JsonOptions);

    internal static IReadOnlyList<string> ReadRepos(string json) =>
        JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
}
