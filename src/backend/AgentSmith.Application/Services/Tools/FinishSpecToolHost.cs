using System.ComponentModel;
using System.Text.Json.Serialization;
using AgentSmith.Application.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-18-98fa: finish_spec — how a spec's coding master tells the pipeline what it acts on: the status, the test
/// ids, the follow-ups, a blocker, the ticket instructions it refused. Typed and checked when the call is made, so a
/// bad argument comes back to the model at once and records nothing. The master's report is its markdown message,
/// written for the person and never parsed. A second call replaces the first, as hand_forward's does.
/// </summary>
public sealed class FinishSpecToolHost : IToolHost
{
    public const string Name = "finish_spec";

    private const string Described =
        "Ends this spec for the pipeline; call it once, after your report. Your report is the markdown message you " +
        "write for the person just before this call — it is shown as you wrote it and never parsed. Pass what the " +
        "pipeline acts on: status 'green' (build clean, tests pass), 'no-tests' (build clean, no automated tests), " +
        "'failed', or 'blocked' with a concrete blocker; summary, one line on what this spec delivered; failing_tests " +
        "and baseline_failing_tests, the test ids failing after and already before the change; follow_ups, each a " +
        "one-line goal and its why; ignored_instructions, ticket instructions you refused, quoted. A second call " +
        "replaces the first.";

    /// <summary>What the last accepted call recorded; null until one was accepted.</summary>
    public MasterVerification? Verification { get; private set; }

    /// <summary>Accepted calls so far — a drive that raised it called finish_spec itself.</summary>
    public int Calls { get; private set; }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(FinishSpec, new AIFunctionFactoryOptions { Name = Name, Description = Described })];
    }

    public string FinishSpec(
        [Description("green | no-tests | failed | blocked.")] string status,
        [Description("One line: what this spec delivered.")] string summary,
        [Description("Test ids failing after the change.")] string[]? failing_tests = null,
        [Description("Test ids that already failed before the change.")] string[]? baseline_failing_tests = null,
        [Description("Proposed follow-up specs, each a one-line goal and why it was left out.")] FinishSpecFollowUp[]? follow_ups = null,
        [Description("What stops the work; required when status is blocked.")] string? blocker = null,
        [Description("Ticket instructions you refused, each quoted.")] string[]? ignored_instructions = null)
    {
        var refusals = Refusals(status, summary, follow_ups, blocker).ToList();
        if (refusals.Count > 0)
            return $"Error: {Name} refused, nothing was recorded: {string.Join("; ", refusals)}.";
        var kind = status.Trim().ToLowerInvariant();
        var blocked = kind == "blocked";
        var built = kind is "green" or "no-tests";
        Verification = new MasterVerification(
            kind switch { "green" => VerificationStatus.Green, "no-tests" => VerificationStatus.NoTests, _ => VerificationStatus.Failed },
            BuildRan: built, BuildPassed: built, TestsRan: kind == "green", TestsPassed: kind == "green",
            Summary: summary.Trim(),
            FailingTests: Clean(failing_tests),
            BaselineFailingTests: Clean(baseline_failing_tests),
            IgnoredInstructions: [.. Clean(ignored_instructions).Select(q => new IgnoredInstruction(q, string.Empty))],
            FollowUps: [.. (follow_ups ?? []).Select(f => new MasterFollowUp(f.Goal!.Trim(), f.Why?.Trim() ?? string.Empty))],
            Blocker: blocked ? blocker!.Trim() : null);
        Calls++;
        return $"Recorded: {kind}, {Verification.FollowUps!.Count} follow-up(s). Your report is the message you wrote "
            + "before this call; end your turn now.";
    }

    private static IEnumerable<string> Refusals(
        string? status, string? summary, FinishSpecFollowUp[]? followUps, string? blocker)
    {
        var kind = status?.Trim().ToLowerInvariant();
        if (kind is not ("green" or "no-tests" or "failed" or "blocked"))
            yield return $"status \"{status}\" is not one of green, no-tests, failed, blocked";
        if (string.IsNullOrWhiteSpace(summary)) yield return "summary is empty";
        if (kind == "blocked" && string.IsNullOrWhiteSpace(blocker)) yield return "a blocked status needs its blocker";
        for (var i = 0; i < (followUps?.Length ?? 0); i++)
            if (string.IsNullOrWhiteSpace(followUps![i]?.Goal)) yield return $"follow_ups entry {i + 1} has no goal";
    }

    private static IReadOnlyList<string> Clean(string[]? entries) =>
        [.. (entries ?? []).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim())];
}

/// <summary>2026-11-18-98fa: one follow-up as finish_spec takes it.</summary>
public sealed class FinishSpecFollowUp
{
    [JsonPropertyName("goal")]
    [Description("What the follow-up spec makes true, in one line.")]
    public string? Goal { get; set; }

    [JsonPropertyName("why")]
    [Description("Why it was left out of this spec.")]
    public string? Why { get; set; }
}
