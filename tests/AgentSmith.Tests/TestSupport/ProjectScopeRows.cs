using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-15-4a4db: the rows a project scope reads — a run, a dashboard conversation, an image of one,
/// and the legacy attachment row an image id not copied yet names.
/// </summary>
internal static class ProjectScopeRows
{
    public static Task RunAsync(AgentSmithDbContext db, string runId, string project) =>
        SaveAsync(db, new Run { Id = runId, Project = project, Pipeline = "fix-bug", TicketId = "T-1", Status = "running" });

    public static Task SessionAsync(
        AgentSmithDbContext db, string sessionId, string threadId, string project,
        string? ticketKey = null, string owner = "someone-else") =>
        SaveAsync(db, new SpecDialogSession
        {
            SessionId = sessionId, Platform = "dashboard", ChannelId = threadId, ThreadId = threadId,
            UserId = owner, Project = project, Tracker = ticketKey is null ? null : "tracker", TicketKey = ticketKey,
        });

    public static Task ImageAsync(AgentSmithDbContext db, long id, string sessionId) =>
        SaveAsync(db, new ReferenceFile
        {
            Id = id, SessionId = sessionId, SetId = $"img-{id}", Kind = ReferenceFileKind.Image,
            RelativePath = "shot.png", MediaType = "image/png", Length = 1, Content = [1],
        });

    public static Task LegacyImageAsync(AgentSmithDbContext db, long id, string sessionId) =>
        SaveAsync(db, new SpecDialogAttachment { Id = id, SessionId = sessionId, MediaType = "image/png", ContentBase64 = "AQ==" });

    private static async Task SaveAsync(AgentSmithDbContext db, object row)
    {
        db.Add(row);
        await db.SaveChangesAsync();
    }
}
