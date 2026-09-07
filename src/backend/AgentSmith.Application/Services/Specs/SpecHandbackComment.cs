using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the ticket comment a hand-back posts. The contradiction case carries the
/// p0318 anchors, so an operator's reply parses back as an answer and re-triggers
/// the run. The VERDICT case carries none on purpose: without an anchor no comment
/// can be read as an answer, so commenting cannot restart a not-implementable
/// ticket — only an explicit operator Retry can.
/// <para>
/// The REFUSED case names what was refused — the quoted sentence and the reason — and
/// how to appeal: reply with why it is legitimate and move the ticket back to a trigger
/// status; the next run's scope call reads the reply beside the ticket text. No spec
/// link: nothing was derived.
/// </para>
/// <para>
/// The QUESTION case lists the readings and names the one the run takes if nobody
/// answers. Its heading carries <see cref="QuestionMarker"/>, so the next run's
/// conversation section keeps the question in view while it is still unanswered.
/// </para>
/// </summary>
public static class SpecHandbackComment
{
    /// <summary>The phrase that marks a question comment as still awaiting an answer.</summary>
    public const string QuestionMarker = "the ticket reads two ways";

    /// <param name="waitingLine">
    /// p0454: who the hand-back waits for, in the platform's mention form. Every case
    /// waits for a person — the contradiction for an answer, the verdict for a Retry,
    /// the refusal for an appeal — so each names one. (Orthogonal to p0448's
    /// AwaitsAnswer, which decides what the NEXT RUN reads back as the requirement,
    /// not who gets told.)
    /// </param>
    public static string Build(SpecHandback handback, string? prUrl, string waitingLine)
    {
        ArgumentNullException.ThrowIfNull(handback);
        var spec = prUrl is null ? string.Empty : $"\n\nThe derived spec is open for review: {prUrl}";
        var waiting = $"\n\n{waitingLine}";
        return handback.Case switch
        {
            SpecHandbackCase.NotImplementable => Verdict(handback) + spec + waiting,
            SpecHandbackCase.Refused => Refused(handback) + waiting,
            SpecHandbackCase.Question => Question(handback) + spec + waiting,
            _ => Contradiction(handback) + spec + waiting,
        };
    }

    /// <summary>The label a reading is listed under: (a), (b), …</summary>
    public static string ReadingLabel(int index) => $"({(char)('a' + index)})";

    private static string Verdict(SpecHandback handback) =>
        "## Agent Smith — not implementable as specified\n\n"
        + handback.Reason
        + "\n\nThis is a verdict, not a question: a comment will not restart the work. "
        + "Change the ticket and use Retry on the run when it should be attempted again.";

    private static string Contradiction(SpecHandback handback) =>
        "## Agent Smith — the requirement contradicts what is in the repository\n\n"
        + handback.Reason;

    private static string Refused(SpecHandback handback)
    {
        var quote = string.IsNullOrWhiteSpace(handback.Quote)
            ? string.Empty
            : $"> {handback.Quote}\n\n";
        return "## Agent Smith — refused: the ticket asks for something that must not be done\n\n"
            + quote + handback.Reason
            + "\n\nNothing was checked out and nothing ran. If this is legitimate, reply with why "
            + "and move the ticket back to a trigger status; the next run reads your reply "
            + "beside the ticket.";
    }

    private static string Question(SpecHandback handback)
    {
        var readings = string.Join("\n", handback.Readings.Select(
            (reading, i) => $"- {ReadingLabel(i)} {reading}"));
        var taken = ReadingLabel(handback.Taken);
        return $"## Agent Smith — {QuestionMarker}, and the work differs\n\n"
            + handback.Reason + "\n\n" + readings
            + $"\n\nReply with the reading you mean and move the ticket back to a trigger status. "
            + $"If nobody answers, the next run proceeds on {taken} and says so on this ticket.";
    }
}
