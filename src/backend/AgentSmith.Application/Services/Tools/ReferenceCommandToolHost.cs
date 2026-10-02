using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-02-075dc: run_in_reference — a shell command in an uploaded set's OWN container, the
/// one its <c>reference:</c> address reads through. That container holds a copy of the upload and
/// nothing else, so the design partner may install, start and inspect there what the material
/// needs; repositories and templates stay read-only, and nothing run here changes what is stored.
/// The command goes through <see cref="SandboxStepRunner.RunAsync"/>, so its timeout clamp and its
/// output are run_command's.
/// </summary>
internal sealed class ReferenceCommandToolHost(
    IReadOnlyDictionary<string, ISandbox> references, RunCommandTimeout timeout) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode) =>
        [AIFunctionFactory.Create(RunInReference, name: "run_in_reference")];

    [Description("Runs a shell command (/bin/sh -c) in the disposable container that holds a COPY of one upload — "
        + "the reference:<name> address — with the working directory /work, where the upload lies under /work/<name>/. "
        + "Use it to find out what the material is and to see it work: list, inspect, install, start, request. "
        + "Nothing run here changes what was uploaded, and repositories stay read-only. python3 with pip is on the "
        + "PATH from a read-only mount: install packages into a venv (python3 -m venv /tmp/venv). Anything else the "
        + "command installs itself (curl a pinned release; apt only where the image runs as root). A browser upload "
        + "carries no file mode, so run a script as `sh script.sh`. Start a server detached (nohup … >/tmp/x.log 2>&1 &) "
        + "or the step waits for it until the timeout. The container may be fresh on any turn: follow the set's recipe "
        + "again rather than assuming an earlier install. Returns exit_code, elapsed_ms, stdout and stderr.")]
    public async Task<string> RunInReference(
        [Description("The upload's address, reference:<name>, as listed under 'Material the operator uploaded'.")] string reference,
        [Description("The shell command.")] string command,
        [Description("Seconds before the command is stopped; clamped to the operator's step cap.")] int? timeout_seconds = null,
        CancellationToken ct = default)
    {
        if (!references.TryGetValue(reference ?? string.Empty, out var sandbox))
            return $"Error: '{reference}' is not an upload of this conversation. Uploads: [{string.Join(", ", references.Keys)}].";
        if (string.IsNullOrWhiteSpace(command)) return "Error: the command is empty.";
        return await new SandboxStepRunner(sandbox, timeout).RunAsync(command, timeout_seconds, ct);
    }
}
