using System.ComponentModel;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-20-9c74: runs ONE verify stage a repository DECLARED, chosen by its LABEL, so a
/// premise about what a gate does is settled by producing its exit code instead of asserted.
/// <para>
/// Nothing the model writes becomes a command. The label picks a line the repository declared
/// on its DEFAULT branch, read before any sandbox existed, so injecting one needs the same
/// write access as changing CI — what CAN be run stays the framework's.
/// </para>
/// <para>
/// What it settles is the tree AS IT STANDS, not what the gate will do: the gate skips a
/// repository whose branch carries no source change, stops at the first non-zero exit, and
/// re-reads an absent-path guard against a tree the phase's work may yet create. A stage may
/// also WRITE into a tree the phase's own commit then stages wholesale.
/// </para>
/// </summary>
public sealed class VerifyStageRunTool(
    DerivationLook look, DerivationLookStages stages, ILogger logger)
{
    public const string Name = "run_verify_stage";

    private bool _spent;
    /// <summary>2026-09-15-ffa7: per look, because the id it spells is the holder's.</summary>
    public string Description =>
        "Runs ONE verify stage this repository DECLARED — by its label, exactly the command "
        + "the declaration carries — and returns the exit code with the end of its output. Use "
        + "it to settle what a build, a test or a lint actually does to the tree as it stands "
        + "now, rather than stating it. Any non-zero exit is a failure. You may run ONE stage "
        + "in total, so spend it on the premise that turns on it. The result starts with an "
        + $"evidence id such as [{look.Terms.EvidencePrefix}4]; a fact resting on it cites that id.";

    public async Task<string> RunVerifyStage(
        [Description("The repository to run in. Use one of the names listed as in scope.")]
        string repository,
        [Description("The LABEL of a stage that repository declared, e.g. 'build'. Only a "
            + "declared label runs; a command you compose is not executed.")]
        string label,
        CancellationToken ct = default)
    {
        if (!look.TryOpen(repository, out var sandbox, out var refusal)) return refusal;
        if (_spent)
            return "One verify stage may be run per check, and it has been run. Settle the rest "
                   + "by reading and searching.";

        var runnable = await stages.RunnableAsync(repository, sandbox, ct);
        var stage = runnable.FirstOrDefault(
            candidate => string.Equals(
                candidate.Stage, label?.Trim(), StringComparison.OrdinalIgnoreCase));
        // Refused WITHOUT charging the one run: the sentence offered the RAW declared labels
        // and the gate's filtering removes some of them only once a sandbox can be asked, so
        // charging for the framework's own omission would spend the only run on a refusal.
        if (stage is null) return NotDeclared(repository, label, runnable);

        _spent = true;
        return await RunAsync(repository, sandbox, stage, ct);
    }

    private async Task<string> RunAsync(
        string repository, ISandbox sandbox, VerifyStage stage, CancellationToken ct)
    {
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: ["-c", stage.Command], WorkingDirectory: stage.Cwd,
            TimeoutSeconds: VerifyCommandRunner.VerifyTimeoutSeconds);
        StepResult result;
        try
        {
            result = await sandbox.RunStepAsync(step, progress: null, ct);
        }
        // A sandbox that goes silent throws from the channel after its wait, and the premise
        // checker turns ANY escaping exception into a lost check. The boundary is here, so a
        // stage that could not run costs one unproven premise instead of the whole check.
        // Guarded on the RUN's token: an operator cancel still propagates.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                ex, "The {Actor} could not run '{Command}' in {Repo}", look.Terms.Actor,
                stage.Command, repository);
            var failed = Mint(repository, stage, SourceScopeLook.NotRunExit, ran: false);
            return $"[{failed}] '{stage.Command}' in {repository} could not be run "
                   + $"({ex.GetType().Name}), so this proves nothing.";
        }

        // RAN means a result came back and it did not time out. The search tool's "0 or 1"
        // convention inverts here: for a gate, 1 is the failure verdict this exists to
        // produce, and the run's own gate calls any non-zero red.
        var ran = !result.TimedOut;
        var id = Mint(repository, stage, result.ExitCode, ran);
        logger.LogInformation(
            "The {Actor} ran the declared {Stage} stage in {Repo} — exit {Exit} as {Id}",
            look.Terms.Actor, stage.Stage, repository, result.ExitCode, id);
        var output = VerifyCommandRunner.Tail(
            Combine(result.OutputContent, result.ErrorMessage), VerifyCommandRunner.OutputTailChars);
        return ran
            ? $"[{id}] '{stage.Command}' in {repository} exited {result.ExitCode}:\n{output}"
            : $"[{id}] '{stage.Command}' in {repository} timed out, so this proves nothing:\n{output}";
    }

    /// <summary>The repository, the DECLARED command and the exit code — and no output, which
    /// keeps the framework's own copy of this line to a command and an exit.</summary>
    private string Mint(string repository, VerifyStage stage, int exitCode, bool ran) =>
        look.Evidence.Remember(new EvidenceRecord(
            repository, EvidenceRecord.VerifyStage, stage.Command, exitCode, ran));

    private static string NotDeclared(
        string repository, string? label, IReadOnlyList<VerifyStage> runnable) =>
        $"{repository} offers no verify stage labelled '{label}'. " + (runnable.Count == 0
            ? "It declares none that can be run here, so nothing was run."
            : $"It offers: {string.Join(", ", runnable.Select(stage => stage.Stage))}.");

    private static string Combine(string? output, string? error) =>
        string.Join("\n", new[] { output, error }.Where(p => !string.IsNullOrWhiteSpace(p))).Trim();
}
