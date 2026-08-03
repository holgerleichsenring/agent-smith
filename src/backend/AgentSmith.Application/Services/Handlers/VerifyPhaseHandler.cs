using CommandLineStringSplitter = System.CommandLine.Parsing.CommandLineStringSplitter;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0393: runs the repository's own build and test commands after the master and
/// fails the run before any PR when either is red.
///
/// p0216 moved build+test verification to the coding master as a RESPONSIBILITY —
/// it runs the repo's tests itself through real run_command calls — and removed the
/// rigid Test step. What it left behind was the absence of a second opinion: nothing
/// refused the pull request when the build was red, so "green" was a claim by the
/// same party that produced the code.
///
/// The commands need no discovery. AnalyzeCode already populates
/// <see cref="CiConfig.BuildCommand"/> and <see cref="CiConfig.TestCommand"/> per repo,
/// and AnalyzeProjectHandler already renders both into its report.
///
/// A repo declaring neither command is SKIPPED, not failed: not every repository in a
/// multi-repo run is buildable (docs, infra, config), and failing them would make the
/// gate unusable exactly where multi-repo runs need it. A run in which NO repo declared
/// a command reports that plainly rather than passing quietly — an unverifiable run must
/// not be indistinguishable from a verified one.
/// </summary>
public sealed class VerifyPhaseHandler(
    ILogger<VerifyPhaseHandler> logger)
    : ICommandHandler<VerifyPhaseContext>
{
    // Build and test on a cold sandbox are the slowest deterministic steps in the run.
    // The sandbox backend still applies the operator's sandbox.step_timeout_seconds cap,
    // so this is a ceiling rather than a second knob.
    private const int VerifyTimeoutSeconds = 1800;

    private const int OutputTailChars = 4000;
    private const int ReasonTailChars = 800;

    public async Task<CommandResult> ExecuteAsync(
        VerifyPhaseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No sandboxes in pipeline context; nothing to verify.");

        var outcomes = new List<VerifyOutcome>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var ci = context.RepoProjectMaps.TryGetValue(key, out var map) ? map.Ci : null;
            var workdir = SubTreeWorkdir(NormalizeWorkdir(
                discoveries.TryGetValue(key, out var discovery) ? discovery.Workdir : null));

            foreach (var (stage, command) in Stages(ci))
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    logger.LogInformation(
                        "{Key}: no {Stage} command discovered — skipping that stage", key, stage);
                    continue;
                }

                var outcome = await RunAsync(key, stage, sandbox, workdir, command!, cancellationToken);
                outcomes.Add(outcome);
                // A red build makes the test result meaningless; stop this repo here so the
                // failure reason names the build rather than a downstream cascade.
                if (outcome.ExitCode != 0) break;
            }
        }

        return BuildAggregateResult(outcomes);
    }

    private static IEnumerable<(string Stage, string? Command)> Stages(CiConfig? ci) =>
        [("build", ci?.BuildCommand), ("test", ci?.TestCommand)];

    private async Task<VerifyOutcome> RunAsync(
        string key, string stage, ISandbox sandbox, string workingDirectory,
        string rawCommand, CancellationToken ct)
    {
        var tokens = CommandLineStringSplitter.Instance.Split(rawCommand).ToList();
        if (tokens.Count == 0)
        {
            logger.LogWarning(
                "{Key}: {Stage} command '{Raw}' tokenized to zero arguments; treating as absent",
                key, stage, rawCommand);
            return new VerifyOutcome(key, stage, rawCommand, ExitCode: 0, Skipped: true);
        }

        logger.LogInformation("{Key}: verifying via {Stage} command '{Command}' at {Cwd}",
            key, stage, rawCommand, workingDirectory);
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: tokens[0], Args: tokens.Skip(1).ToArray(),
            WorkingDirectory: workingDirectory, TimeoutSeconds: VerifyTimeoutSeconds);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        var output = Combine(result.OutputContent, result.ErrorMessage);

        if (result.ExitCode != 0)
            // Surface WHY on the spot: the operator sees a red run, and without the tail
            // the only way to learn what broke is to reproduce the whole run.
            logger.LogError("{Key}: {Stage} '{Command}' failed (exit {Exit}) at {Cwd}:\n{Output}",
                key, stage, rawCommand, result.ExitCode, workingDirectory, Tail(output, OutputTailChars));

        return new VerifyOutcome(key, stage, rawCommand, result.ExitCode, Skipped: false, Output: output);
    }

    private static CommandResult BuildAggregateResult(IReadOnlyList<VerifyOutcome> outcomes)
    {
        var ran = outcomes.Where(o => !o.Skipped).ToList();
        if (ran.Count == 0)
            return CommandResult.Ok(
                "Nothing to verify: no repository declared a build or test command. "
                + "This run is UNVERIFIED — add ci.build_command / ci.test_command to make the gate real.");

        var failed = ran.Where(o => o.ExitCode != 0).ToList();
        if (failed.Count == 0)
            return CommandResult.Ok($"Verified: {Describe(ran)} green");

        var first = failed[0];
        var detail = Tail(first.Output, ReasonTailChars);
        var where = string.IsNullOrEmpty(first.Key) ? "(default)" : first.Key;
        var reason = string.IsNullOrWhiteSpace(detail) ? string.Empty : $"\n{detail}";
        return CommandResult.Fail(
            $"Verification failed: {where} {first.Stage} '{first.Command}' exited {first.ExitCode}."
            + $" No pull request is opened for a red build.{reason}");
    }

    private static string Describe(IReadOnlyList<VerifyOutcome> ran) =>
        string.Join(", ", ran
            .GroupBy(o => string.IsNullOrEmpty(o.Key) ? "(default)" : o.Key)
            .Select(g => $"{g.Key} [{string.Join('+', g.Select(o => o.Stage))}]"));

    private static string SubTreeWorkdir(string workdir) =>
        workdir == "." ? Repository.SandboxWorkPath : $"{Repository.SandboxWorkPath}/{workdir}";

    private static string NormalizeWorkdir(string? workdir)
    {
        if (string.IsNullOrWhiteSpace(workdir)) return ".";
        var trimmed = workdir.Trim().Replace('\\', '/').Trim('/');
        return trimmed.Length == 0 ? "." : trimmed;
    }

    private static string Combine(string? stdout, string? stderr) =>
        string.Join('\n', new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string Tail(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty
        : text.Length <= max ? text
        : text[^max..];

    private sealed record VerifyOutcome(
        string Key, string Stage, string Command, int ExitCode, bool Skipped, string Output = "");
}
