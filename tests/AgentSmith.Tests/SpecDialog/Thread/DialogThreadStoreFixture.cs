using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.Thread;

/// <summary>A migrated in-memory store and the values the thread tests build their turns from.</summary>
internal sealed class DialogThreadStoreFixture : IDisposable
{
    public const string Session = "s-thread";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    /// <summary>A fresh context on the same store, as a new scope gets.</summary>
    public AgentSmithDbContext Context() => MigratedStoreTemplate.Context(_connection);

    public static ConversationState State(params TranscriptTurn[] transcript) => new()
    {
        JobId = Session, ChannelId = Session, UserId = "U1", Platform = "dashboard", Project = "sample",
        TicketId = Session, StartedAt = DateTimeOffset.UtcNow, Mode = ConversationMode.SpecDialog,
        ThreadId = Session, Transcript = transcript,
    };

    public static TranscriptTurn User(string text) => new(TranscriptRole.User, text, DateTimeOffset.UtcNow);

    public static TranscriptTurn Assistant(string text, SpecDialogTurnKind kind = SpecDialogTurnKind.Answer) =>
        new(TranscriptRole.Assistant, text, DateTimeOffset.UtcNow, kind);

    public static ForwardedThreadCapture Capture(string answered)
    {
        var capture = new ForwardedThreadCapture();
        capture.Record([new ChatMessage(ChatRole.User, "the prompt")], new ChatResponse(new ChatMessage(ChatRole.Assistant, answered)));
        capture.Answered(answered);
        return capture;
    }

    public static SpecDialogTurnResult Result(string reply, ForwardedThreadCapture capture,
        SpecDialogTurnKind kind = SpecDialogTurnKind.Answer) =>
        new(reply, new AnswerOutcome(), reply, kind, Thread: capture);

    public void Dispose() => _connection.Dispose();
}
