using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0315d: dogfoods the methodology — writes the executed phase spec to
/// <c>.agentsmith/phases/done/{phaseId}-{slug}.yaml</c> in every repo's
/// sandbox working tree (mirroring WriteRunResultHandler's per-repo record
/// fan-out), so CommitAndPR force-stages it with the change set and the
/// target repo carries the same planned→done record this project lives.
/// </summary>
public sealed partial class WritePhaseRecordHandler(
    ISandboxFileReaderFactory readerFactory,
    Contracts.Specs.ISpecSetWriter specSetWriter,
    ILogger<WritePhaseRecordHandler> logger)
    : ICommandHandler<WritePhaseRecordContext>
{
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugRegex();

    public async Task<CommandResult> ExecuteAsync(
        WritePhaseRecordContext context, CancellationToken cancellationToken)
    {
        // Absent spec is a composition bug: this step only runs inside the
        // phase-execution preset, where PhaseSpecGate always publishes it.
        // p0393: the step now runs in the ONE code-changing preset, which handles ordinary
        // tickets too — and an ordinary ticket carries no phase spec. There is nothing to
        // record then, and demanding one would fail every bug and feature run.
        if (!context.Pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft) || draft is null)
            return CommandResult.Ok("No phase spec on this run; nothing to record");
        var relativePath = Path.Combine(
            ".agentsmith", "phases", "done", $"{draft.PhaseId}-{Slug(draft.Goal)}.yaml");

        var repos = context.Pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is { Count: > 0 } ? r : null;
        if (repos is null)
        {
            var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
            await WriteAsync(sandbox, context.Repository.LocalPath, relativePath, draft, cancellationToken);
            return CommandResult.Ok($"Phase record {relativePath} written (single sandbox)");
        }

        var written = 0;
        foreach (var repo in repos)
        {
            var matches = SandboxTargets.SandboxesForRepo(context.Pipeline, repo);
            if (matches.Count == 0)
            {
                logger.LogWarning("WritePhaseRecord: no sandbox for repo '{Repo}' — skipping", repo.Name);
                continue;
            }
            await WriteAsync(matches[0].Value, context.Repository.LocalPath, relativePath, draft, cancellationToken);
            written++;
        }
        await MarkExecutedAsync(context, repos, draft, cancellationToken);
        return CommandResult.Ok($"Phase record {relativePath} written in {written} repo(s)");
    }

    /// <summary>
    /// p0393a: records on the BRANCH that this phase ran. An executed phase is
    /// append-only — a later comment may re-cut the unexecuted tail but never rewrite a
    /// phase whose work is already in the branch history — and the next run can only
    /// honour that if the branch says which phases those are.
    /// </summary>
    private async Task MarkExecutedAsync(
        WritePhaseRecordContext context, IReadOnlyList<RepoConnection>? repos,
        PhaseDraft draft, CancellationToken ct)
    {
        if (!context.Pipeline.TryGet<Contracts.Specs.SpecSet>(ContextKeys.SpecSet, out var set)
            || set is null)
            return;
        if (set.Executed.Contains(draft.PhaseId, StringComparer.Ordinal)) return;

        var updated = set with { Executed = [.. set.Executed, draft.PhaseId] };
        context.Pipeline.Set(ContextKeys.SpecSet, updated);

        var carrier = CarryingRepo(context, repos);
        if (carrier is null) return;
        var write = await specSetWriter.WriteAsync(context.Pipeline, carrier, updated, ct);
        if (!write.Written)
            logger.LogWarning(
                "Phase {PhaseId} ran but the branch could not record it as executed: {Error}",
                draft.PhaseId, write.Error);
    }

    private static RepoConnection? CarryingRepo(
        WritePhaseRecordContext context, IReadOnlyList<RepoConnection>? repos)
    {
        if (repos is not { Count: > 0 }) return null;
        return context.Pipeline.TryGet<string>(ContextKeys.SpecRepo, out var name)
            && !string.IsNullOrWhiteSpace(name)
                ? repos.FirstOrDefault(
                    r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) ?? repos[0]
                : repos[0];
    }

    private async Task WriteAsync(
        ISandbox sandbox, string repoLocalPath, string relativePath, PhaseDraft draft, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        await reader.WriteAsync(Path.Combine(repoLocalPath, relativePath), draft.Yaml.TrimEnd() + "\n", ct);
        logger.LogInformation("Phase record {PhaseId} written to {Path}", draft.PhaseId, relativePath);
    }

    internal static string Slug(string goal)
    {
        var slug = NonSlugRegex().Replace(goal.ToLowerInvariant(), "-").Trim('-');
        return slug.Length <= 60 ? slug : slug[..60].TrimEnd('-');
    }
}
