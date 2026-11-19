using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-18-9c1cd: the coding master's run_command (run_gate_stage), log_decision (decide_by_default) and finish_spec (hand_forward).</summary>
public static partial class MergedTools
{
    /// <summary>run_command with run_gate_stage folded in: a shell command, or a declared verify stage by label.</summary>
    public static AIFunction CodingRunCommand(FilesystemToolHost fs, GateStageToolHost gate, Contracts.Commands.PipelineContext pipeline) =>
        AIFunctionFactory.Create(
            async ([Description("Shell command to execute (passed to /bin/sh -c). Omit when you run a stage.")] string? command = null,
                   [Description("Repo whose sandbox runs it. Required on multi-repo projects; optional on single-repo.")] string? repo = null,
                   [Description("Optional timeout in seconds (default 60, max 600) for a command.")] int? timeout_seconds = null,
                   [Description("The LABEL of a verify stage a context of this run declared, e.g. 'build' or 'test': it runs exactly the declared command in that context's own sandbox, after every sandbox of the run is levelled to the work so far. Use it for a stage of another toolchain image than the repo's first sandbox.")] string? stage = null,
                   [Description("The context that declares the stage; needed only when two contexts of the repo declare the label.")] string? context = null,
                   CancellationToken ct = default) =>
            {
                if (!string.IsNullOrWhiteSpace(stage))
                {
                    if (!string.IsNullOrWhiteSpace(command)) return "Error: pass either command or stage, not both.";
                    if (string.IsNullOrWhiteSpace(repo)) return "Error: a stage needs the repo that declares it.";
                    var declared = GateStageToolHost.Declared(pipeline);
                    if (declared.Count == 0) return "Error: no context of this run declares a verify stage; run the command itself.";
                    return await gate.RunAsync(pipeline, repo, stage, context, ct);
                }
                if (string.IsNullOrWhiteSpace(command)) return "Error: pass a command, or a stage label.";
                return await fs.RunCommand(command, repo, timeout_seconds, ct);
            },
            "run_command",
            "Runs a shell command (/bin/sh -c) in the named repo's ephemeral sandbox — build, test, git, sed, curl, "
            + "scripts: the sandbox is an isolated checkout and all work lands as a reviewable branch. Or pass stage "
            + "(and repo) to run a verify stage a context of this run DECLARED, exactly as declared, in that context's "
            + "own sandbox; any non-zero exit is red. Returns exit_code, elapsed_ms, stdout and stderr.");

    /// <summary>log_decision with decide_by_default folded in as <c>overrulable</c>.</summary>
    public static AIFunction LogDecision(LogDecisionToolHost log, DefaultDecisionToolHost? defaults) =>
        AIFunctionFactory.Create(
            async ([Description("Category: Architecture, Tooling, Implementation, or TradeOff.")] string category,
                   [Description("One line: the decision and its rationale; with overrulable, what you chose.")] string decision,
                   [Description("The repository it is about. Required when the run has more than one repository.")] string? repository = null,
                   [Description("True records a choice a person may overrule later but need not make before work continues: you go on with it at once, and it waits for a person to ratify or overrule it. Needs question, alternatives and why. A choice that needs a person BEFORE work continues is ask_human.")] bool overrulable = false,
                   [Description("With overrulable: the question the choice answers.")] string? question = null,
                   [Description("With overrulable: the one to eight alternatives you did not choose.")] string[]? alternatives = null,
                   [Description("With overrulable: why this choice, in a sentence or two.")] string? why = null,
                   [Description("With overrulable: true holds the next automatic attempt of the series until a person looks; it never stops this run.")] bool holds_series = false,
                   CancellationToken ct = default) =>
            {
                if (!overrulable) return await log.LogDecision(category, decision, repository, ct);
                if (defaults is null)
                    return "Error: nothing was recorded — this master runs without a run id, so a default has no one to ratify it. Log it without overrulable, or ask with ask_human.";
                return await defaults.DecideByDefault(question ?? string.Empty, decision, alternatives ?? [], why ?? string.Empty, holds_series, ct);
            },
            "log_decision",
            "Records a key architectural, tooling, implementation or trade-off decision on the run. With overrulable: "
            + "true it is a default a person may overrule later — recorded for ratification once, not logged twice.");


    /// <summary>finish_spec with hand_forward's build note folded in.</summary>
    public static AIFunction FinishSpec(FinishSpecToolHost finish, HandForwardToolHost? forward) =>
        AIFunctionFactory.Create(
            async ([Description("green | no-tests | failed | blocked.")] string status,
                   [Description("One line: what this spec delivered.")] string summary,
                   [Description("Test ids failing after the change.")] string[]? failing_tests = null,
                   [Description("Test ids that already failed before the change.")] string[]? baseline_failing_tests = null,
                   [Description("Proposed follow-up specs, each a one-line goal and why it was left out.")] FinishSpecFollowUp[]? follow_ups = null,
                   [Description("What stops the work; required when status is blocked.")] string? blocker = null,
                   [Description("Ticket instructions you refused, each quoted.")] string[]? ignored_instructions = null,
                   [Description("Build note: names this spec renamed, each '<old> -> <new>'.")] string[]? renamed = null,
                   [Description("Build note: files or members moved, each '<old> -> <new>'.")] string[]? moved = null,
                   [Description("Build note: what this spec persisted that a later spec reads (tables, columns, files, keys).")] string[]? persisted = null,
                   [Description("Build note: where this spec deviated from its text, and why.")] string[]? deviations = null,
                   [Description("The stored memories this spec acted on, each 'slug' or 'repository/slug' ('@project/slug' for a project memory).")] string[]? relied_on = null,
                   CancellationToken ct = default) =>
            {
                // A call that names no build-note field leaves the note an earlier call recorded.
                if (forward is not null && (renamed ?? moved ?? persisted ?? deviations ?? relied_on) is not null)
                {
                    var note = await forward.HandForward(renamed, moved, persisted, deviations, relied_on, ct);
                    if (note.StartsWith("Error", StringComparison.Ordinal)) return note.Replace("hand_forward", "finish_spec", StringComparison.Ordinal);
                    var finished = finish.FinishSpec(status, summary, failing_tests, baseline_failing_tests, follow_ups, blocker, ignored_instructions);
                    return finished.StartsWith("Error", StringComparison.Ordinal) ? finished : $"{note}\n{finished}";
                }
                return finish.FinishSpec(status, summary, failing_tests, baseline_failing_tests, follow_ups, blocker, ignored_instructions);
            },
            FinishSpecToolHost.Name,
            "Ends this spec for the pipeline; call it once, after your report. Your report is the markdown message you "
            + "write for the person just before this call — shown as written, never parsed. Pass what the pipeline acts "
            + "on: status 'green' (build clean, tests pass), 'no-tests' (build clean, no automated tests), 'failed', or "
            + "'blocked' with its blocker; summary; failing and baseline-failing test ids; follow_ups; ignored_instructions. "
            + "The build note for the next spec of the series rides along: renamed and moved ('<old> -> <new>'), persisted, "
            + "deviations (at most 20 entries of 300 characters each), and relied_on, the memories you acted on — a memory "
            + "is a hint with a track record, and what becomes of this spec becomes its record. A second call replaces the first.");
}
