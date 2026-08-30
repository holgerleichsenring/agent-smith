using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-08-30-3c12: decides whether an answer's evidence resolves, and says what it is
/// worth when it does not.
/// <para>
/// The evidence is the run's OWN read set, settled with
/// <see cref="ReadPathNormalizer.WasRead"/> — the rule that decides whether a finding may
/// call itself analyzed-from-source. Reading the cited file back out of the sandbox would
/// bless the exact miss this track exists to catch: a file the scan never opened is still
/// perfectly readable afterwards. What must hold is that the scan READ it.
/// </para>
/// <para>
/// A group-wide claim is settled against every member it names. It is the strongest claim a
/// scan can make — "no entry point here is anonymous" — and it has no line of its own, so
/// without that rule it would be the cheapest claim to fabricate rather than the dearest.
/// </para>
/// </summary>
public static class RequirementCitation
{
    private const int NamedInCitation = 3;

    public static (RequirementDisposition Disposition, string Citation, string Note) Settle(
        RequirementAnswer answer, IReadOnlyCollection<string>? read)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (answer.Disposition == RequirementDisposition.CannotAnswer)
            return (answer.Disposition, string.Empty,
                $"cannot answer without {answer.MissingInput ?? "an input it did not name"}");
        return answer.Scope == RequirementScope.GroupWide
            ? GroupWide(answer, read)
            : Member(answer, read);
    }

    private static (RequirementDisposition, string, string) Member(
        RequirementAnswer answer, IReadOnlyCollection<string>? read)
    {
        if (string.IsNullOrWhiteSpace(answer.File) || answer.StartLine <= 0)
            return (RequirementDisposition.Unanswered, string.Empty,
                "the answer cites no file and line");
        return ReadPathNormalizer.WasRead(read, answer.File)
            ? (answer.Disposition, $"{answer.File}:{answer.StartLine}", string.Empty)
            : (RequirementDisposition.Unanswered, string.Empty,
                $"cites {answer.File}, which this scan never read");
    }

    private static (RequirementDisposition, string, string) GroupWide(
        RequirementAnswer answer, IReadOnlyCollection<string>? read)
    {
        if (answer.Members.Count == 0)
            return (RequirementDisposition.Unanswered, string.Empty,
                "a claim about the whole group cites none of the members it generalises over");
        var unread = answer.Members.Where(m => !ReadPathNormalizer.WasRead(read, m)).ToList();
        return unread.Count > 0
            ? (RequirementDisposition.Unanswered, string.Empty,
                $"generalises over {Name(unread)}, which this scan never read")
            : (answer.Disposition, $"covers {answer.Members.Count} member(s): {Name(answer.Members)}",
                string.Empty);
    }

    private static string Name(IReadOnlyList<string> members) =>
        string.Join(", ", members.Take(NamedInCitation))
        + (members.Count > NamedInCitation ? ", ..." : string.Empty);
}
