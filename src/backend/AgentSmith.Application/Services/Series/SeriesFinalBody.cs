using System.Text;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-9992i: the body of a series' final pull request — what the person reviewing the series at its end reads. A
/// section per ticket: what was built (its title and pull requests), what the series master did on it and why (the act log,
/// 9992e), and what it could not verify — a pull request whose CI on its last head was not green or never read. A GitHub
/// issue keeps its closing keyword, so merging the final pull request closes it.
/// </summary>
public static class SeriesFinalBody
{
    public static string Of(SeriesRunRecord record, string branch, IReadOnlyList<SeriesTicketFact> facts)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(facts);
        var text = new StringBuilder($"Delivers series {record.Id} from `{branch}` into the default branch: its tickets landed "
            + "there one by one, each merged by the series master.\n");
        foreach (var ticket in record.Tickets)
            Section(text, ticket, facts.FirstOrDefault(f => f.Ticket.TicketId == ticket.TicketId), record.Acts);
        return text.ToString().TrimEnd();
    }

    private static void Section(StringBuilder text, SeriesTicket ticket, SeriesTicketFact? fact, IReadOnlyList<SeriesAct> acts)
    {
        text.Append($"\n## {ticket.Key}: {ticket.Title}\n\n");
        if (ticket.Key.StartsWith('#')) text.Append($"Closes {ticket.Key}\n\n");
        var pullRequests = fact?.PullRequests ?? [];
        text.Append(pullRequests.Count == 0 ? "- Pull requests: none read\n"
            : $"- Pull requests: {string.Join(", ", pullRequests.Select(p => $"{p.Repo} [#{p.Number}]({p.Url})"))}\n");
        var mine = acts.Where(a => string.Equals(a.Ticket, ticket.TicketId, StringComparison.Ordinal)).ToList();
        text.Append(mine.Count == 0 ? "- The series master did not act on it\n" : "- The series master:\n");
        foreach (var act in mine) text.Append($"  - {act.Text}{(act.Reason is { Length: > 0 } why ? $" — {why}" : string.Empty)}\n");
        text.Append(Unverified(pullRequests));
    }

    private static string Unverified(IReadOnlyList<SeriesRunPullRequestView> pullRequests)
    {
        var open = pullRequests.Where(p => p.Ci != CiStates.Green).Select(p => $"{p.Repo} ({p.Ci ?? "not read"})").ToList();
        return open.Count == 0 ? "- Verified: CI green on every pull request's last head\n"
            : $"- Not verified: CI on the last head of {string.Join(", ", open)}\n";
    }
}
