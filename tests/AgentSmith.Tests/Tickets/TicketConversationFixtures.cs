using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-9a59h: the rows a ticket's conversation is read from, as their writers leave them.</summary>
internal static class TicketConversationFixtures
{
    public static Run Attempt(string id, int actsReadMinutes, string status = "failed")
    {
        var run = Run(id, status: status);
        run.ActsReadAt = T.AddMinutes(actsReadMinutes);
        return run;
    }

    public static RunConversationEntry Row(
        string runId, long seq, string kind, int minutes, string? text = null, string? questionId = null, string? detailJson = null) =>
        new() { RunId = runId, EventSeq = seq, Kind = kind, At = T.AddMinutes(minutes), Text = text, QuestionId = questionId, DetailJson = detailJson };

    public static RunAttempt Fetched(string runId, string fingerprint) => new() { RunId = runId, TicketFingerprint = fingerprint };

    public static DialogueAnswerEntry Answer(string runId, string questionId, string answer, string by, string? comment, int minutes) =>
        new() { DialogueJobId = runId, QuestionId = questionId, Answer = answer, AnsweredBy = by, Comment = comment, AnsweredAt = T.AddMinutes(minutes) };

    public static string PrUrl(string number) => $"https://github.com/o/api/pull/{number}";

    public static PrReviewRecord Record(string number, string state = PrReviewStates.Merged) => new()
    {
        PrKey = PrKey.Of(PrUrl(number)), Url = PrUrl(number), Repo = "api", Number = number, Project = "alpha", TicketId = "42",
        RunId = "r-1", State = state, StateAtTicks = T.UtcTicks, CheckedAtTicks = T.AddHours(3).UtcTicks,
    };

    public static PrReviewEntry Note(
        string number, string hostId, string body, int minutes, string? author = "bob", string kind = PrReviewEntryKinds.Comment,
        bool ours = false, bool system = false, string? decision = null, string? file = null, int? line = null) => new()
    {
        PrKey = PrKey.Of(PrUrl(number)), HostId = hostId, Kind = kind, Decision = decision, Standing = decision is not null,
        AuthorLogin = author, Trusted = true, IsOurs = ours, IsSystem = system, File = file, Line = line, Body = body,
        AtTicks = T.AddMinutes(minutes).UtcTicks,
    };

    public static TicketComment Comment(string id, string author, int minutes, string body) =>
        new(author, T.AddMinutes(minutes), body) { Id = id };

    public static SpecDialogSession Session(OutcomeProposal? proposal = null, params TranscriptTurn[] turns) => new()
    {
        SessionId = "s-1", Platform = "dashboard", ChannelId = "c", ThreadId = "d-1", UserId = "owner", Project = "alpha",
        Tracker = "gh", TicketKey = "github-42", IsOpen = true,
        TranscriptJson = SpecDialogSessionMapper.WriteTranscript(turns),
        LatestProposalJson = proposal is null ? null : OutcomeProposalJson.Write(proposal),
    };

    public static TranscriptTurn Turn(TranscriptRole role, string text, int minutes) => new(role, text, T.AddMinutes(minutes));
}
