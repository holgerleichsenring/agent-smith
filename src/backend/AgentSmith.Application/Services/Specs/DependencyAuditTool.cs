using System.ComponentModel;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: the ecosystem's own advisory command with fixed arguments, and its
/// structured output handed back whole. This is the look the dependency ticket was one
/// call away from: "one direct package is affected in one project and eight in the
/// other" is the audit's own answer, and with it in hand the right criterion writes
/// itself. Nothing is installed and no shell is involved; an absent tool is reported as
/// proving nothing.
/// </summary>
public sealed class DependencyAuditTool(
    DerivationLook look, ISandboxFileReaderFactory files,
    IPackageEcosystemDetector ecosystems, ILogger logger)
{
    public const string Name = "audit_dependencies";

    /// <summary>The audit's JSON, bounded: a large graph's report is read for its
    /// counts and its direct packages, not for every advisory paragraph.</summary>
    public const int MaxChars = 12_000;

    [Description("Runs the repository's own dependency advisory command with fixed "
                 + "arguments — 'dotnet list package --vulnerable --format json' (DIRECT "
                 + "references only, transitive ones are not listed), 'npm audit --json', or "
                 + "'pip-audit --format=json' — and returns its JSON. Use it before stating "
                 + "anything about vulnerable, outdated or direct packages. Read-only. The "
                 + "result starts with an evidence id such as [L1]; a fact that rests on this "
                 + "audit cites that id. Exit 0 means nothing found, 1 means findings; any "
                 + "other exit means the tool could not run and the result proves nothing.")]
    public async Task<string> AuditDependencies(
        [Description("The repository to audit. Use one of the names listed as in scope.")]
        string repository,
        CancellationToken ct = default)
    {
        if (!look.TryOpen(repository, out var sandbox, out var refusal)) return refusal;

        var ecosystem = await ecosystems.DetectAsync(
            files.Create(sandbox), Repository.SandboxWorkPath, ct);
        var step = ecosystem is null ? null : AuditCommands.For(ecosystem);
        if (step is null)
            return $"{repository} declares no ecosystem with an advisory command here"
                   + $"{(ecosystem is null ? string.Empty : $" ({ecosystem.Kind})")}. This proves nothing.";

        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        var ran = AuditCommands.ReachedAVerdict(result.ExitCode) && !result.TimedOut;
        var id = look.Evidence.Remember(repository, AuditCommands.Describe(step), result.ExitCode, ran);
        logger.LogInformation(
            "The derivation audited {Repo} ({Ecosystem}) — exit {Exit} as {Id}",
            repository, ecosystem!.Kind, result.ExitCode, id);
        return Report(id, repository, step, result, ran);
    }

    private static string Report(string id, string repository, Step step, StepResult result, bool ran)
    {
        var output = BoundedResultTool.Bound((result.OutputContent ?? string.Empty).Trim(), MaxChars);
        var what = $"[{id}] {AuditCommands.Describe(step)} in {repository}";
        return ran
            ? $"{what} exited {result.ExitCode}:\n{output}"
            : $"{what} could not run (exit {result.ExitCode}) and proves nothing: {output}";
    }
}
