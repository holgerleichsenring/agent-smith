using System.Text;
using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: the body of the PR opened at the spec commit. It says plainly that the
/// run is still working and points at the two files a reviewer edits — the edit
/// is a commit on the branch, not an "input", and the next revision treats it as
/// such.
/// </summary>
public static class WorkSpecPrBody
{
    public static string Build(WorkSpecArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var key = new WorkSpecKey(artifact.Spec.Key);
        var sb = new StringBuilder();
        sb.AppendLine("> 🚧 **Work in progress** — this PR was opened at the work-spec commit so the");
        sb.AppendLine("> specification can be reviewed while the run is still working. Edit");
        sb.AppendLine($"> `{key.SpecPath}` or `{key.SamplesPath}` here and the next revision takes your");
        sb.AppendLine("> change as its input instead of overwriting it.");
        sb.AppendLine();
        sb.AppendLine($"## {artifact.Spec.Goal}");
        AppendList(sb, "Requirements", artifact.Spec.Requirements);
        AppendList(sb, "Constraints", [.. artifact.Spec.Constraints.Select(c => c.Rule)]);
        AppendList(sb, "Assumptions", artifact.Spec.Assumptions);
        return sb.ToString();
    }

    private static void AppendList(StringBuilder sb, string heading, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine($"### {heading}");
        foreach (var entry in entries) sb.AppendLine($"- {entry}");
    }
}
