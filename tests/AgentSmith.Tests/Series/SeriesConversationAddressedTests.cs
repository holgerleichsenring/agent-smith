using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>The series conversation's reads from the stored transcript. 2026-11-25-e94eb: it no longer tells a dashboard
/// conversation from a chat one — no series conversation carries a status table.</summary>
public sealed class SeriesConversationAddressedTests : IDisposable
{
    private readonly ServerStateStore _store = new();

    // 2026-11-23-31dch: the message addressed to the series carries its poster, who answers a relayed question.
    [Fact]
    public async Task SeriesConversation_Addressed_CarriesThePoster()
    {
        await using var context = _store.Context();
        context.Add(new SpecDialogSession
        {
            SessionId = "s-1", Platform = "dashboard", ChannelId = "c-1", ThreadId = "d-1", UserId = "u-1", Project = "sample",
            IsOpen = true, LastActivityAt = DateTimeOffset.UnixEpoch,
            TranscriptJson = SpecDialogSessionMapper.WriteTranscript(
            [
                new TranscriptTurn(TranscriptRole.User, "the v2 payload", DateTimeOffset.UnixEpoch, Author: "Dana", SeriesRunId: "sr-1"),
                new TranscriptTurn(TranscriptRole.User, "elsewhere", DateTimeOffset.UnixEpoch, Author: "Eve"),
            ]),
        });
        await context.SaveChangesAsync();
        var conversation = new SeriesConversation(new SpecDialogSessionRepository(context), null!);

        (await conversation.AddressedAsync("s-1", "sr-1", -1, default)).Should().ContainSingle()
            .Which.Should().Be(new AgentSmith.Contracts.Series.SeriesOperatorMessage(0, "the v2 payload", DateTimeOffset.UnixEpoch, "Dana"));
    }

    public void Dispose() => _store.Dispose();
}
