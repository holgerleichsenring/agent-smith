using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-9a59e: cut, spec and attempt rows as the projections write them.</summary>
internal static class TicketSeriesFixtures
{
    public static SpecCutEntry Entry(string specId, int ordinal, params string[] done) =>
        new(specId, ordinal, $"{specId}-label", $"Goal of {specId}", ["api"],
            [new SpecStep("s1", "Add the endpoint", "api:src/A.cs")], done, $"spec: {specId}\n");

    public static RunSeriesCut Cut(string runId, long seq, params SpecCutEntry[] specs) => new()
    {
        RunId = runId, EventSeq = seq, SetKey = "github-42", SpecsJson = RunStoryJson.Serialize(specs),
        At = TicketStoreHarness.T.AddMinutes(seq),
    };

    public static RunSeriesCut Handback(string runId, long seq, string handbackCase)
    {
        var cut = Cut(runId, seq);
        cut.HandbackCase = handbackCase;
        return cut;
    }

    public static RunSpec SpecRow(string runId, string specId, string status, string? acceptanceJson = null) => new()
    {
        RunId = runId, SpecId = specId, Ordinal = 1, Title = specId, Status = status,
        StartedAt = status == "not_started" ? null : TicketStoreHarness.T,
        EndedAt = status is "done" or "failed" ? TicketStoreHarness.T.AddMinutes(20) : null,
        AcceptanceJson = acceptanceJson,
    };

    public static RunAttempt Attempt(string runId, int revision, string cause, params string[] carried) => new()
    {
        RunId = runId, RevisionNumber = revision, RevisionCause = cause,
        CarriedSpecIdsJson = System.Text.Json.JsonSerializer.Serialize(carried),
    };

    public static string Acceptance(params (string Text, string Status)[] criteria) =>
        RunStoryJson.Serialize(new AcceptanceView(
            [.. criteria.Select(c => new AcceptanceCriterionView(c.Text, c.Status, $"why {c.Status}", "src/A.cs"))],
            ExpectationOutcomes.Verbatim, "the ratified spec", AcceptanceSources.DeliveryAccount));
}
