using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>2026-11-17-ec48a: what revise_spec answers the coding master — written, or why not.</summary>
public static class SpecRevisionReplies
{
    public static string NotRevised(string why) => $"Not revised: {why}. The spec stands as it was.";

    /// <param name="movedFrom">2026-11-27-6625: the repositories the spec was written into before, when it moved.</param>
    public static string Written(SpecDraft spec, int revision, IReadOnlyList<string>? movedFrom = null) =>
        $"Spec {spec.SpecId} is revised: revision {revision} is on the ticket branch. "
        + (movedFrom is null ? string.Empty
            : $"It is written into {string.Join(", ", spec.Repos)} now, its spec record with it: your file writes there land and its checks run "
                + $"there. What {string.Join(", ", movedFrom)} already holds on its branch, the earlier spec copy and any "
                + "work, stays as it is. ")
        + "These are its done criteria now; "
        + "your report answers each of them:\n"
        + string.Join("\n", spec.Done.Select(d => "- " + d))
        + "\nRestructure your checklist with update_progress to the revised steps, and verify each criterion above "
        + "before your verdict.";
}
