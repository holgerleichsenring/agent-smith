using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: whether a stored hand-back has EXPIRED — a scoped repository's branch got a
/// commit by somebody other than us since it parked. A contradiction is a claim about the code;
/// once somebody changed the code, replaying the stored claim without a model call answers a
/// question nobody is asking any more.
/// <para>
/// PROBE ONLY A HANDED-BACK SET: our own provider-API commits (filing) carry the token's identity,
/// not ours, and would read as foreign. OURS BY EMAIL: what the checkout merged from the base is
/// excluded twice — merge commits by <c>--no-merges</c>, fast-forwarded base commits by
/// <c>--not &lt;base&gt;</c> — and what is left is foreign when its author is not
/// <see cref="SandboxGitIdentity.Email"/>. A sandbox that commits under another email, a
/// repository with no stored head, or a head this clone does not know is SKIPPED and logged.
/// </para>
/// </summary>
public sealed class ForeignCommitProbe(
    SandboxTargets targets, SandboxBaseLadder ladder, ILogger<ForeignCommitProbe> logger)
{
    private const int TimeoutSeconds = 120;

    /// <summary>Probes every scoped repository when the branch set is handed back, and records the
    /// answer under <see cref="ContextKeys.ForeignCommitSinceHandback"/> for the hand-back step.</summary>
    public async Task<bool> ProbeAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos, SpecSet? onBranch,
        SpecSetPointer? pointer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(repos);
        var foreign = onBranch is { IsHandedBack: true }
            && await AnyForeignAsync(pipeline, repos, pointer?.HandbackHeads, cancellationToken);
        pipeline.Set(ContextKeys.ForeignCommitSinceHandback, foreign);
        return foreign;
    }

    /// <summary>True when HEAD carries a commit since <paramref name="stored"/> that neither came
    /// from the base nor was authored by us.</summary>
    public async Task<bool> ForeignSinceAsync(
        ISandbox sandbox, string repository, string? stored, string? parentTicketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        if (string.IsNullOrWhiteSpace(stored)) return Skipped(repository, "no head was stored with the hand-back");
        var email = (await RunAsync(sandbox, ["config", "--get", "user.email"], ct)).OutputContent?.Trim();
        if (!string.Equals(email, SandboxGitIdentity.Email, StringComparison.OrdinalIgnoreCase))
            return Skipped(repository, $"the sandbox commits as '{email}', so our commits cannot be told apart");
        if ((await ladder.ResolveAsync(sandbox, parentTicketId, ct)).Ref is not { } baseRef)
            return Skipped(repository, "the clone names no base branch");
        var listed = await RunAsync(
            sandbox, ["rev-list", "--no-merges", "--format=%ae", $"{stored}..HEAD", "--not", baseRef], ct);
        if (listed.ExitCode != 0) return Skipped(repository, $"the stored head {stored} is unknown to this clone");
        var authors = (listed.OutputContent ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("commit ", StringComparison.Ordinal))
            .Where(author => !string.Equals(author, SandboxGitIdentity.Email, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (authors.Count == 0) return false;
        logger.LogInformation("{Repo} got commits by {Authors} since the hand-back parked at {Sha} — it expires",
            repository, string.Join(", ", authors), stored);
        return true;
    }

    private async Task<bool> AnyForeignAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos,
        IReadOnlyDictionary<string, string>? heads, CancellationToken ct)
    {
        var parent = RunParentTicket.Of(pipeline);
        foreach (var repo in repos)
        {
            var name = repo.Name ?? string.Empty;
            var sandbox = targets.SandboxesForRepo(pipeline, repo).Select(m => m.Value).FirstOrDefault();
            if (sandbox is null) Skipped(name, "the run has no sandbox for it");
            else if (await ForeignSinceAsync(sandbox, name, heads?.GetValueOrDefault(name), parent, ct)) return true;
        }
        return false;
    }

    private bool Skipped(string repository, string why)
    {
        logger.LogInformation("Foreign-commit probe skipped {Repo}: {Why}", repository, why);
        return false;
    }

    private static Task<StepResult> RunAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct) =>
        sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git", Args: args,
                WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: TimeoutSeconds),
            progress: null, ct);
}
