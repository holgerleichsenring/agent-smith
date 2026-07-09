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
        var draft = context.Pipeline.Get<PhaseDraft>(ContextKeys.PhaseSpec);
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
        return CommandResult.Ok($"Phase record {relativePath} written in {written} repo(s)");
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
