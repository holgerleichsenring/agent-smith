using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-10-4778: a design turn's run_command — a shell in a throwaway work clone of the commit the turn's reads
/// show, for the master only. Builds run one at a time, nothing is pushed, and the clone is gone after the turn.
/// </summary>
internal sealed class DesignWorkToolHost(
    WorkCloneSet clones, IReadOnlyDictionary<string, ISandbox> heldScopes, RunCommandTimeout timeout) : IToolHost
{
    public const string Full = "full";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(RunCommand, name: "run_command")];
    }

    [Description("Runs a shell command (/bin/sh -c) in your work clone of one repository: a fresh clone of the commit "
        + "your reads show, on the repository's toolchain image, with /work as the repository root. Use it to check a "
        + "claim — git log -p, build, run the tests, start the prototype. The clone has the last 50 commits; pass "
        + "history: full on the first command for a repository when you need more (asked for later, it starts a new "
        + "full clone and what you changed in the old one is gone). Edit files here with the shell to "
        + "try something; the clone is yours alone, nothing in it is pushed or kept, and it is gone when this turn "
        + "ends. Cite what the read tools show, not what you changed here. Returns exit_code, elapsed_ms, stdout and stderr.")]
    public async Task<string> RunCommand(
        [Description("The shell command.")] string command,
        [Description("The repository, by the name the read tools use.")] string repo,
        [Description("'full' for the whole history, best on the first command for this repository; omit otherwise.")] string? history = null,
        [Description("Seconds before the command is stopped; clamped to the operator's step cap.")] int? timeout_seconds = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command)) return "Error: the command is empty.";
        if (history is not null && !string.Equals(history, Full, StringComparison.OrdinalIgnoreCase))
            return $"Error: history takes 'full' or nothing, not '{history}'.";
        ISandbox clone;
        try
        {
            clone = await clones.ForAsync(repo ?? string.Empty, history is not null, heldScopes, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SourceScopeUnavailableException)
        {
            return $"Error: {ex.Message}";
        }
        return await new SandboxStepRunner(clone, timeout).RunAsync(command, timeout_seconds, ct);
    }
}
