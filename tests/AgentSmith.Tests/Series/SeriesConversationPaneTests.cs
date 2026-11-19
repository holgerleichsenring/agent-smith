using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Series;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-19-1b72a: a conversation held on the dashboard has the Series pane; one on a chat platform has none,
/// so it keeps the status table.</summary>
public sealed class SeriesConversationPaneTests : IDisposable
{
    private readonly ServerStateStore _store = new();

    [Theory]
    [InlineData("dashboard", true)]
    [InlineData("slack", false)]
    public async Task SeriesConversation_HasPane_OnlyOnTheDashboard(string platform, bool pane)
    {
        await using var context = _store.Context();
        context.Add(new SpecDialogSession
        {
            SessionId = "s-1", Platform = platform, ChannelId = "c-1", ThreadId = "d-1", UserId = "u-1", Project = "sample",
            IsOpen = true, LastActivityAt = DateTimeOffset.UnixEpoch,
        });
        await context.SaveChangesAsync();
        var conversation = new SeriesConversation(new SpecDialogSessionRepository(context), null!);

        (await conversation.HasPaneAsync("s-1", default)).Should().Be(pane);
        (await conversation.HasPaneAsync("s-unknown", default)).Should().BeFalse();
    }

    public void Dispose() => _store.Dispose();
}
