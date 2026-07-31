using System.Text;
using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: the work spec rendered for the run detail — the current revision plus
/// the revision LIST with each revision's cause, so the viewer answers "what is
/// this run working toward, and what changed it" without leaving the dashboard.
/// The content of record is still the branch; this is the viewer's copy.
/// </summary>
public static class WorkSpecMarkdown
{
    public static string Render(WorkSpecArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var spec = artifact.Spec;
        var key = new WorkSpecKey(spec.Key);
        var sb = new StringBuilder();
        sb.AppendLine($"# {spec.Goal}");
        sb.AppendLine();
        sb.AppendLine($"`{key.SpecPath}` — revision {spec.Current.Number} ({spec.Current.Cause})");
        AppendList(sb, "Requirements", spec.Requirements);
        AppendConstraints(sb, spec);
        AppendDone(sb, spec);
        AppendList(sb, "Assumptions", spec.Assumptions);
        AppendHandback(sb, spec);
        AppendRevisions(sb, spec);
        return sb.ToString();
    }

    private static void AppendConstraints(StringBuilder sb, WorkSpec spec) =>
        AppendList(sb, "Constraints (verbatim from the ticket)",
            [.. spec.Constraints.Select(c => c.SampleAnchor is null
                ? c.Rule
                : $"{c.Rule} _(sample: `{c.SampleAnchor}` in spec.md)_")]);

    private static void AppendDone(StringBuilder sb, WorkSpec spec)
    {
        var heading = spec.DoneIsReadOnly
            ? "Done-criteria (the ratified acceptance contract — read-only)"
            : "Done-criteria";
        AppendList(sb, heading, spec.Done);
    }

    private static void AppendHandback(StringBuilder sb, WorkSpec spec)
    {
        if (spec.Handback is not { } handback) return;
        sb.AppendLine();
        sb.AppendLine($"## Handed back — {handback.Case}");
        sb.AppendLine(handback.Reason);
    }

    private static void AppendRevisions(StringBuilder sb, WorkSpec spec)
    {
        sb.AppendLine();
        sb.AppendLine("## Revisions");
        foreach (var revision in spec.Revisions)
            sb.AppendLine(
                $"- **{revision.Number}** — {revision.Cause} "
                + $"({revision.At:yyyy-MM-dd HH:mm} UTC)");
    }

    private static void AppendList(StringBuilder sb, string heading, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine($"## {heading}");
        foreach (var entry in entries) sb.AppendLine($"- {entry}");
    }
}
