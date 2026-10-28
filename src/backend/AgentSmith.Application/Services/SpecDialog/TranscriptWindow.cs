using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// The part of a long conversation a dialog turn's prompt carries. Up to <see cref="VerbatimChars"/> the transcript
/// is rendered whole, as it always was; past that, the latest digest stands in for the turns it folded, the pinned
/// ones among them stay verbatim, and every later turn follows. A fold is due only once the verbatim tail outgrows the
/// bound, and it folds down to <see cref="KeepRecentChars"/>, so a fold follows at least that much new conversation.
/// </summary>
public static class TranscriptWindow
{
    public const int VerbatimChars = 60_000;
    public const int KeepRecentChars = 30_000;
    public const int MinRecentTurns = 2;
    public const int MaxFoldChars = 200_000;

    public static string Line(SpecDialogTurn turn) =>
        turn.Author is { } author ? $"[{turn.Role}: {author}] {turn.Text}" : $"[{turn.Role}] {turn.Text}";

    public static string Render(IReadOnlyList<SpecDialogTurn> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        var full = Join(turns);
        if (LatestDigest(turns) is not { } digest || full.Length <= VerbatimChars) return full;
        var through = digest.DigestThrough!.Value;
        var pinned = Pinned(turns);
        var kept = Enumerable.Range(0, turns.Count)
            .Where(i => !IsDigest(turns[i]) && (i > through || pinned.Contains(i)))
            .Select(i => turns[i]).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"[Turns 1 to {through + 1} are summarised in the digest below; the pinned ones among them "
            + "follow it verbatim, then every later turn.]");
        sb.AppendLine(digest.Text);
        sb.Append(Join(kept));
        return sb.ToString().TrimEnd();
    }

    /// <summary>The fold that is due, or null: the verbatim tail is at most <see cref="VerbatimChars"/>, or only the
    /// newest <see cref="MinRecentTurns"/> would be left to fold.</summary>
    public static TranscriptFold? Fold(IReadOnlyList<SpecDialogTurn> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);
        var digest = LatestDigest(turns);
        var after = digest?.DigestThrough ?? -1;
        var tail = Enumerable.Range(after + 1, Math.Max(0, turns.Count - after - 1))
            .Where(i => !IsDigest(turns[i])).ToList();
        var rest = tail.Sum(i => Width(turns[i]));
        if (rest <= VerbatimChars) return null;
        var folded = new List<int>();
        var input = 0;
        foreach (var index in tail)
        {
            var width = Width(turns[index]);
            if (rest <= KeepRecentChars || tail.Count - folded.Count <= MinRecentTurns) break;
            if (folded.Count > 0 && input + width > MaxFoldChars) break;
            folded.Add(index);
            input += width;
            rest -= width;
        }
        return folded.Count == 0
            ? null
            : new TranscriptFold(folded[0], folded[^1], [.. folded.Select(i => turns[i])], digest?.Text);
    }

    private static HashSet<int> Pinned(IReadOnlyList<SpecDialogTurn> turns)
    {
        var pinned = new HashSet<int>();
        var firstOperator = -1;
        var newestProposal = -1;
        SpecDialogTurn? lastAssistant = null;
        for (var i = 0; i < turns.Count; i++)
        {
            var turn = turns[i];
            if (IsDigest(turn)) continue;
            if (turn.Role == SpecDialogTurn.UserRole)
            {
                if (firstOperator < 0) firstOperator = i;
                if (lastAssistant is { Kind: { } kind } && IsProposal(kind)) pinned.Add(i);
                continue;
            }
            lastAssistant = turn;
            if (turn.Kind is SpecDialogTurnKind.Filing or SpecDialogTurnKind.MockRatified) pinned.Add(i);
            if (turn.Kind is { } own && IsProposal(own)) newestProposal = i;
        }
        if (firstOperator >= 0) pinned.Add(firstOperator);
        if (newestProposal >= 0) pinned.Add(newestProposal);
        return pinned;
    }

    private static bool IsProposal(SpecDialogTurnKind kind) => kind is SpecDialogTurnKind.BugProposal
        or SpecDialogTurnKind.SpecProposal or SpecDialogTurnKind.EpicProposal or SpecDialogTurnKind.TicketGraphProposal or SpecDialogTurnKind.CommentProposal;

    private static bool IsDigest(SpecDialogTurn turn) => turn.Kind == SpecDialogTurnKind.Digest && turn.DigestThrough is not null;

    private static SpecDialogTurn? LatestDigest(IReadOnlyList<SpecDialogTurn> turns) => turns.LastOrDefault(IsDigest);

    private static int Width(SpecDialogTurn turn) => Line(turn).Length + Environment.NewLine.Length;

    private static string Join(IEnumerable<SpecDialogTurn> turns)
    {
        var sb = new StringBuilder();
        foreach (var turn in turns) sb.AppendLine(Line(turn));
        return sb.ToString().TrimEnd();
    }
}
