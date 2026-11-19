using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>2026-11-17-ec48a: what revise_spec answers the coding master — written, or why not.</summary>
public static class SpecRevisionReplies
{
    public static string NotRevised(string why) => $"Not revised: {why}. The spec stands as it was.";

    public static string Written(SpecDraft spec, int revision) =>
        $"Spec {spec.SpecId} is revised: revision {revision} is on the ticket branch. These are its done criteria now; "
        + "your report answers each of them:\n"
        + string.Join("\n", spec.Done.Select(d => "- " + d))
        + "\nRestructure your checklist with update_progress to the revised steps, and verify each criterion above "
        + "before your verdict.";
}
