using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199d: presets whose chain runs Triage + CompileDiscussion (autonomous,
/// skill-manager) publish ContextKeys.Plan without going through FetchTicket
/// — so WriteRunResultHandler's WriteSingleAsync path NREs on Ticket.Title.
/// Seed a stub Ticket so the tail of the pipeline writes its run.md instead
/// of failing on the missing handle. Harness-only concern; the production
/// path always has a ticket because autonomous runs are trigger-driven.
/// </summary>
internal static class HarnessTicketSeed
{
    public static void SeedIfPlanProducing(PipelineContext pipeline, string presetName)
    {
        if (!IsPlanProducingPreset(presetName)) return;
        pipeline.Set(ContextKeys.Ticket, new Ticket(
            id: new TicketId("harness-stub"),
            title: "Harness stub trigger",
            description: "Synthetic ticket so WriteRunResult has a slug source.",
            acceptanceCriteria: null,
            status: "open",
            source: "harness",
            labels: Array.Empty<string>()));
    }

    private static bool IsPlanProducingPreset(string presetName) =>
        string.Equals(presetName, "autonomous", StringComparison.OrdinalIgnoreCase)
        || string.Equals(presetName, "skill-manager", StringComparison.OrdinalIgnoreCase);
}
