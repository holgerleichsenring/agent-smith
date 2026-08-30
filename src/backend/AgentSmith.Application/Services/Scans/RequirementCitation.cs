using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-08-30-3c12: decides whether a cited finding's evidence resolves, and says what it
/// is worth when it does not.
/// <para>
/// The evidence is the run's OWN read set, settled with
/// <see cref="ReadPathNormalizer.WasRead"/> — the rule that decides whether a finding may
/// call itself analyzed-from-source. Reading the cited file back out of the sandbox would
/// bless the exact miss this track exists to catch: a file the scan never opened is still
/// perfectly readable afterwards. What must hold is that the scan READ it.
/// </para>
/// <para>
/// A group-wide finding is settled against every member it names. It is the strongest claim
/// a scan can make — "none of these entry points checks who is asking" — and it has no line
/// of its own, so without that rule it would be the cheapest claim to fabricate rather than
/// the dearest.
/// </para>
/// </summary>
public static class RequirementCitation
{
    private const int NamedInCitation = 3;

    public static CitedFindingRow Settle(CitedFinding finding, IReadOnlyCollection<string>? read)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var (located, citation, note) = finding.Scope == RequirementScope.GroupWide
            ? GroupWide(finding, read)
            : Member(finding, read);
        return new CitedFindingRow(
            finding.Station, finding.RequirementId, finding.Level, finding.Text, finding.Detail,
            located, citation, note);
    }

    private static (bool, string, string) Member(
        CitedFinding finding, IReadOnlyCollection<string>? read)
    {
        if (string.IsNullOrWhiteSpace(finding.File) || finding.StartLine <= 0)
            return (false, string.Empty, "the finding cites no file and line");
        return ReadPathNormalizer.WasRead(read, finding.File)
            ? (true, $"{finding.File}:{finding.StartLine}", string.Empty)
            : (false, string.Empty, $"cites {finding.File}, which this scan never read");
    }

    private static (bool, string, string) GroupWide(
        CitedFinding finding, IReadOnlyCollection<string>? read)
    {
        if (finding.Members.Count == 0)
            return (false, string.Empty,
                "a finding about the whole group cites none of the members it generalises over");
        var unread = finding.Members.Where(m => !ReadPathNormalizer.WasRead(read, m)).ToList();
        return unread.Count > 0
            ? (false, string.Empty, $"generalises over {Name(unread)}, which this scan never read")
            : (true, $"covers {finding.Members.Count} member(s): {Name(finding.Members)}",
                string.Empty);
    }

    private static string Name(IReadOnlyList<string> members) =>
        string.Join(", ", members.Take(NamedInCitation))
        + (members.Count > NamedInCitation ? ", ..." : string.Empty);
}
