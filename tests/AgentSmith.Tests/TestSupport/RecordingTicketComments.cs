using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-17-0e79b: every comment a run posts through one provider factory, recorded. Two of
/// these sit on the harness — one behind the kept-set notice and one behind the cut commenter —
/// so a test can count either without the other's comments in the way, and so the cut comment is
/// actually POSTED rather than swallowed by a bare mock whose Create returns null.
/// </summary>
internal sealed class RecordingTicketComments
{
    internal List<string> Comments { get; } = [];

    /// <summary>Set to make the tracker refuse a comment, which is what leaves an input uncleared.</summary>
    internal bool Refuse { get; set; }

    internal TrackerConnection Tracker { get; } =
        new() { Name = "tracker", Type = TrackerType.AzureDevOps };

    internal ITicketProviderFactory Factory()
    {
        var provider = new Mock<ITicketProvider>();
        provider
            .Setup(p => p.UpdateStatusAsync(
                It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((TicketId _, string body, CancellationToken _) =>
            {
                if (Refuse) throw new InvalidOperationException("the tracker refused the comment");
                Comments.Add(body);
                return Task.CompletedTask;
            });
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        return factory.Object;
    }
}
