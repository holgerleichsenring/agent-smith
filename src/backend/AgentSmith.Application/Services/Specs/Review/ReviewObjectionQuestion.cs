using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762h: the one question the objections to ask become — a <see cref="SpecHandbackCase.Question"/>
/// of kind <see cref="SpecQuestionKind.ReviewObjection"/>, because the taken reading and the pin need the
/// Question case. The reason names each objection by its <see cref="ReviewObjectionComment.MarkerFor"/> line,
/// with every finding and the evidence line it rests on; building the spec as reviewed is the reading taken
/// when nobody answers. Unlike a reading question it does not clear the specs: they are what it asks about.
/// </summary>
public static class ReviewObjectionQuestion
{
    public const string BuildAsReviewed = "build the spec as reviewed; the objection stays on record";
    public const string ChangeAsAsked = "change the spec as the objection asks";

    public static SpecHandback For(IReadOnlyList<StandingObjection> objections)
    {
        ArgumentNullException.ThrowIfNull(objections);
        return new SpecHandback(SpecHandbackCase.Question, Reason(objections), Readings: [BuildAsReviewed, ChangeAsAsked],
            Taken: 0) { QuestionKind = SpecQuestionKind.ReviewObjection };
    }

    private static string Reason(IReadOnlyList<StandingObjection> objections)
    {
        var sb = new StringBuilder("After its review rounds a fresh reviewer that opened the code still refuses "
            + "to build what follows. Only the author can say whether it is built as reviewed or changed.\n");
        foreach (var objection in objections)
        {
            var subject = objection.SpecId is null ? " (the series)" : string.Empty;
            sb.Append($"\n{ReviewObjectionComment.MarkerFor(objection.MarkerKey)}{subject}:\n");
            if (objection.Findings.Count == 0) sb.Append("- the reviewer named no finding\n");
            foreach (var finding in objection.Findings) sb.Append($"- {finding.Text} — {finding.Evidence}\n");
        }
        return sb.ToString().TrimEnd();
    }
}
