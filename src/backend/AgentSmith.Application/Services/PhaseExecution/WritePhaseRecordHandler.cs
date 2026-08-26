using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
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
/// <para>
/// 2026-08-26-31e5: and the <c>state.done</c> line that NAMES that file. The pointer has
/// shipped since p0315d and the index never did, so the chronicle existed as loose files
/// nobody assembled and a repository agent-smith had worked in for months still described
/// only its first day. Every repo that gets the pointer gets the line.
/// </para>
/// </summary>
public sealed partial class WritePhaseRecordHandler(
    ISandboxFileReaderFactory readerFactory,
    ExecutedPhaseMarker executedPhases,
    PhaseRecordPublisher publisher,
    PhaseRecordIndexLine indexLine,
    PhaseIndexWriter indexWriter,
    SandboxTargets sandboxTargets,
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

        var line = indexLine.Compose(draft.Goal, relativePath.Replace('\\', '/'));
        if (line is null)
            return CommandResult.Fail(
                $"Phase record pointer alone exceeds {PhaseRecordIndexLine.MaxChars} characters: {relativePath}");

        var body = Specs.PhaseRecordBody.For(draft, context.Pipeline);
        var repos = context.Pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is { Count: > 0 } ? r : null;
        await publisher.PublishAsync(context.Pipeline, draft, body, cancellationToken);
        if (repos is null)
        {
            var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
            await WriteAsync(sandbox, context.Repository.LocalPath, relativePath, body, cancellationToken);
            await indexWriter.WriteAsync(
                context.Pipeline, sandbox, null, context.Repository.LocalPath,
                draft.PhaseId, line, cancellationToken);
            return CommandResult.Ok($"Phase record {relativePath} written (single sandbox)");
        }

        var written = 0;
        var indexed = 0;
        foreach (var repo in repos)
        {
            var matches = sandboxTargets.SandboxesForRepo(context.Pipeline, repo);
            if (matches.Count == 0)
            {
                logger.LogWarning("WritePhaseRecord: no sandbox for repo '{Repo}' — skipping", repo.Name);
                continue;
            }
            await WriteAsync(matches[0].Value, context.Repository.LocalPath, relativePath, body, ct: cancellationToken);
            written++;
            if (await indexWriter.WriteAsync(
                    context.Pipeline, matches[0].Value, matches[0].Key, context.Repository.LocalPath,
                    draft.PhaseId, line, cancellationToken))
                indexed++;
        }
        await executedPhases.MarkAsync(context.Pipeline, repos, draft, cancellationToken);
        return CommandResult.Ok(
            $"Phase record {relativePath} written in {written} repo(s), indexed in {indexed}");
    }

    private async Task WriteAsync(
        ISandbox sandbox, string repoLocalPath, string relativePath, string body, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        await reader.WriteAsync(Path.Combine(repoLocalPath, relativePath), body, ct);
        logger.LogInformation("Phase record written to {Path}", relativePath);
    }

    internal static string Slug(string goal)
    {
        var slug = NonSlugRegex().Replace(goal.ToLowerInvariant(), "-").Trim('-');
        return slug.Length <= 60 ? slug : slug[..60].TrimEnd('-');
    }
}
