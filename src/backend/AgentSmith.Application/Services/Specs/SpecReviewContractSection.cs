using System.Text;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762k: the series contract as the diff reviewer reads it — a rule source beside the principles. The
/// entries naming the spec under review come first, then the rest, up to the cap; past it the reviewer is told it
/// was shown in part. A diff breaks the contract by using what ANOTHER member owns as often as by missing its own.
/// </summary>
public static class SpecReviewContractSection
{
    public static string Render(SeriesContract? contract, string specId, int maxChars)
    {
        if (contract is null || contract.IsEmpty) return string.Empty;
        var lines = SeriesContractPromptSection.Lines(contract);
        var ordered = lines.Where(l => l.Members.Contains(specId)).Concat(lines.Where(l => !l.Members.Contains(specId)));
        var sb = new StringBuilder();
        var whole = true;
        foreach (var line in ordered)
        {
            if (sb.Length + line.Text.Length + 3 > maxChars)
            {
                whole = false;
                break;
            }
            sb.Append("- ").AppendLine(line.Text);
        }
        return $"\n## The series contract {specId} belongs to\nA finding may quote one of these entries as its rule: a "
            + "datum persisted outside its home, a name or number another member owns, a file outside this spec's "
            + "area, a line budget passed.\n" + sb
            + (whole ? string.Empty : "(shown in part — the entries naming this spec first; quote no entry you were not shown)\n");
    }
}
