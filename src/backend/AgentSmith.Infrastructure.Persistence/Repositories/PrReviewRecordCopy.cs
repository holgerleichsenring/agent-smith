using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-13-9a59b: a refreshed read copied onto the stored rows, column by column, so the change
/// tracker writes only what differs. A note that once carried a decision keeps it; whether the
/// decision still stands is <see cref="PrReviewEntry.Standing"/>.
/// </summary>
internal static class PrReviewRecordCopy
{
    public static void Record(PrReviewRecord read, PrReviewRecord stored)
    {
        Stamp(read, stored);
        stored.Url = read.Url;
        stored.Repo = read.Repo;
        stored.RepoKey = read.RepoKey;
        stored.Number = read.Number;
        stored.Project = read.Project;
        stored.TicketId = read.TicketId;
        stored.RunId = read.RunId;
        stored.State = read.State;
        stored.StateAtTicks = read.StateAtTicks;
        stored.HeadSha = read.HeadSha ?? stored.HeadSha;
    }

    // 2026-10-15-114fc: taken before the columns are overwritten; a read without a sha neither moves nor erases the head.
    private static void Stamp(PrReviewRecord read, PrReviewRecord stored)
    {
        if (read.HeadSha is { } head && stored.HeadSha is { } was && head != was) stored.HeadMovedAtTicks = read.CheckedAtTicks;
        if (PrReviewStates.IsEnded(stored.State) && read.State == PrReviewStates.Open)
            stored.ReopenedAtTicks = read.CheckedAtTicks;
    }

    public static void Entry(PrReviewEntry read, PrReviewEntry stored)
    {
        stored.Kind = read.Kind;
        stored.Decision = read.Decision ?? stored.Decision;
        stored.Standing = read.Standing;
        stored.AuthorLogin = read.AuthorLogin;
        stored.Trusted = read.Trusted;
        stored.IsOurs = read.IsOurs;
        stored.IsBot = read.IsBot;
        stored.IsSystem = read.IsSystem;
        stored.File = read.File;
        stored.Line = read.Line;
        stored.Resolved = read.Resolved;
        stored.ThreadHostId = read.ThreadHostId;
        stored.Body = read.Body;
        stored.Truncated = read.Truncated;
        stored.AtTicks = read.AtTicks;
    }
}
