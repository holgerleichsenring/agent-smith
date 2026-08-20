using System.ComponentModel;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0482: a read-only search of the branch, for the delivery account to run ITSELF.
/// <para>
/// Five phases fixed the FORM of a fact copied into a budgeted list and copied back out
/// again — p0469 got the agent's commands to the account, p0470 stopped the list dropping
/// them, p0473, p0474 and p0481 fixed how one is cited. Every one was a RETRIEVAL failure:
/// the reader needed a fact that existed and had no way to reach it.
/// </para>
/// <para>
/// An account that can search the branch does not need a cited command, a reach argument or
/// a citation format for the criterion class that has cost the most runs: proving something
/// is not there. It is also STRONGER evidence than the agent could offer, because the
/// agent's commands ran at some point during the work while this runs against what the
/// branch carries now — which is already the tie-break the account's prompt states.
/// </para>
/// <para>
/// This opens no channel to the agent that did the work. The account stays a fresh instance
/// with no account of the work, because a model that believes it did the work confirms
/// itself. Asking the author to argue is persuasion; looking at the branch is not.
/// </para>
/// </summary>
public sealed class BranchSearch(
    IReadOnlyDictionary<string, ISandbox> sandboxes, ILogger logger)
{
    private const int TimeoutSeconds = 90;
    private const int MaxOutputChars = 4_000;

    /// <summary>The repositories a search may name, for the prompt to list.</summary>
    public IReadOnlyList<string> Repositories => [.. sandboxes.Keys];

    /// <summary>p0482: how many searches one account may run. An account that cannot settle a
    /// criterion in this many looks is not going to, and every search is a sandbox
    /// round-trip inside a model call at the end of a run.</summary>
    public const int MaxSearches = 12;

    private int _ran;

    [Description("Searches the branch as it stands now, in one repository, and returns the "
                 + "matching lines with their file and line number. This is how you settle a "
                 + "criterion about something being ABSENT: search for it and see. No output "
                 + "means the branch does not contain it. Read-only.")]
    public async Task<string> SearchBranch(
        [Description("The repository to search. Use one of the names listed under COMMANDS.")]
        string repository,
        [Description("An extended regular expression, e.g. 'MassTransit|IMessageBus\\.InvokeAsync'.")]
        string pattern,
        [Description("Optional path within the repository to search under. Omit to search all of it.")]
        string? path = null,
        CancellationToken ct = default)
    {
        if (Interlocked.Increment(ref _ran) > MaxSearches)
            return $"No search left — an account may run {MaxSearches}. Decide on what you have.";
        if (!sandboxes.TryGetValue(repository, out var sandbox))
            return $"No repository named '{repository}'. The branch carries: {string.Join(", ", sandboxes.Keys)}.";
        if (string.IsNullOrWhiteSpace(pattern))
            return "A search needs a pattern.";

        var result = await RunAsync(sandbox, pattern, path, ct);
        logger.LogInformation(
            "The delivery account searched {Repo} for {Pattern} under {Path} — exit {Exit}",
            repository, pattern, path ?? ".", result.ExitCode);
        return Report(result, repository, pattern, path);
    }

    /// <summary>grep with a fixed argument vector and no shell: the pattern is one argv
    /// element, so nothing a model writes can become a command. Read-only is a property of
    /// what CAN be run here, not of what the caller is asked to stick to.</summary>
    private static async Task<StepResult> RunAsync(
        ISandbox sandbox, string pattern, string? path, CancellationToken ct)
    {
        string[] args =
        [
            "-RInE", "--binary-files=without-match",
            "--exclude-dir=bin", "--exclude-dir=obj", "--exclude-dir=.git",
            "--exclude-dir=node_modules", "-e", pattern, "--",
            string.IsNullOrWhiteSpace(path) ? "." : path,
        ];
        return await sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "grep", Args: args, WorkingDirectory: "/work",
                TimeoutSeconds: TimeoutSeconds),
            progress: null, ct);
    }

    /// <summary>grep exits 1 BECAUSE it found nothing, and that is the proof an absence
    /// criterion asks for — so the status is reported as a finding, never as a failure. An
    /// exit above 1 is grep itself failing, which proves nothing at all.</summary>
    private static string Report(StepResult result, string repository, string pattern, string? path)
    {
        var where = $"{repository}{(string.IsNullOrWhiteSpace(path) ? string.Empty : "/" + path)}";
        var output = (result.OutputContent ?? string.Empty).Trim();
        return result.ExitCode switch
        {
            0 => $"'{pattern}' found in {where}:\n"
                 + (output.Length <= MaxOutputChars ? output : output[..MaxOutputChars] + "\n… more matches follow"),
            1 => $"'{pattern}' does not occur anywhere in {where}.",
            _ => $"The search of {where} could not run (exit {result.ExitCode}) and proves nothing: {output}",
        };
    }
}
