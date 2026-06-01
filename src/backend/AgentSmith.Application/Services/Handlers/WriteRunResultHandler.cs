using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Writes run artifacts (plan.md + result.md) to
/// <c>.agentsmith/runs/{RunId}-{slug}/</c> and appends the run entry under the
/// top-level <c>runs:</c> key in <c>.agentsmith/context.yaml</c> (creating that
/// key when absent — no silent skip). Formatting is delegated to
/// RunResultFormatter. RunId is generated once at pipeline start by
/// ExecutePipelineUseCase and read from PipelineContext here.
///
/// p0161d: init-project runs fan out per <see cref="RepoConnection"/>: each
/// repo's sandbox gets its own <c>runs/{runId}-init/{plan.md,result.md}</c>
/// with that repo's slice of <see cref="ContextKeys.DiscoveredComponents"/>
/// and <see cref="ContextKeys.BootstrapOutputs"/>. Same RunId across all
/// repos. Ticket-driven runs keep the single-sandbox path.
/// </summary>
public sealed class WriteRunResultHandler(
    ISandboxFileReaderFactory readerFactory,
    IDialogueTrail dialogueTrail,
    IRunArtifactStore artifactStore,
    ILogger<WriteRunResultHandler> logger)
    : ICommandHandler<WriteRunResultContext>
{
    private const string AgentSmithDir = ".agentsmith";
    private const string RunsDir = "runs";
    private const string ContextFileName = "context.yaml";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<CommandResult> ExecuteAsync(
        WriteRunResultContext context, CancellationToken cancellationToken)
    {
        var runId = context.Pipeline.Get<string>(ContextKeys.RunId);
        if (IsInitMode(context))
            return await WriteInitFanOutAsync(context, runId, cancellationToken);
        return await WriteSingleAsync(context, runId, cancellationToken);
    }

    private static bool IsInitMode(WriteRunResultContext context) =>
        context.Ticket is null && context.Plan is null;

    private async Task<CommandResult> WriteSingleAsync(
        WriteRunResultContext context, string runId, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var runsDir = Path.Combine(agentDir, RunsDir);
        var slug = GenerateSlug(context.Ticket!.Title);
        var runDir = Path.Combine(runsDir, $"{runId}-{slug}");

        // p0196: post-p0179b coding presets retired GeneratePlan; Plan is
        // null then. Skip plan.md in that case rather than NRE on the
        // formatter. Presets that still set Plan (scan / mad / autonomous)
        // continue to emit plan.md.
        if (context.Plan is not null)
        {
            var planMd = RunResultFormatter.FormatPlan(context.Ticket, context.Plan);
            await reader.WriteAsync(Path.Combine(runDir, "plan.md"), planMd, cancellationToken);
        }
        await WriteOptionalArtifactsAsync(reader, runDir, context.Pipeline, cancellationToken);

        var (cost, duration) = ResolveCostAndDuration(context.Pipeline);
        var trail = TryGet<List<ExecutionTrailEntry>>(context.Pipeline, ContextKeys.ExecutionTrail);
        var decisions = TryGet<List<PlanDecision>>(context.Pipeline, ContextKeys.Decisions);
        var trend = TryGet<SecurityTrend>(context.Pipeline, ContextKeys.SecurityTrend);
        var dialogueEntries = dialogueTrail.GetAll();
        var perSkillBreakdown = ResolvePerSkillBreakdown(context.Pipeline);
        var topology = ResolveTopology(context.Pipeline, runId);

        var resultMd = RunResultFormatter.FormatResult(
            context.Ticket, context.Plan, context.Changes,
            runId, duration, cost, trail, decisions, trend,
            dialogueEntries.Count > 0 ? dialogueEntries : null, perSkillBreakdown,
            topology);
        await reader.WriteAsync(Path.Combine(runDir, "result.md"), resultMd, cancellationToken);
        await TryStoreResultAsync(runId, resultMd, cancellationToken);

        await AppendToContextYamlAsync(
            reader, Path.Combine(agentDir, ContextFileName), runId, context.Ticket, cancellationToken);
        logger.LogInformation(
            "Written run result {RunId} to {Dir}", RunIdGenerator.FormatForDisplay(runId), Path.GetFileName(runDir));
        return CommandResult.Ok(
            $"Run {RunIdGenerator.FormatForDisplay(runId)} recorded in {Path.GetFileName(runDir)}");
    }

    private async Task<CommandResult> WriteInitFanOutAsync(
        WriteRunResultContext context, string runId, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is { Count: > 0 } ? r : null;
        if (repos is null)
            return await WriteInitSingleSandboxAsync(context, runId, cancellationToken);

        var components = TryGet<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
            pipeline, ContextKeys.DiscoveredComponents);
        var outputs = TryGet<Dictionary<string, Dictionary<string, string>>>(
            pipeline, ContextKeys.BootstrapOutputs);
        var (cost, duration) = ResolveCostAndDuration(pipeline);
        var trail = TryGet<List<ExecutionTrailEntry>>(pipeline, ContextKeys.ExecutionTrail);
        var decisions = TryGet<List<PlanDecision>>(pipeline, ContextKeys.Decisions);
        var dialogueEntries = dialogueTrail.GetAll();
        var perSkillBreakdown = ResolvePerSkillBreakdown(pipeline);
        var sharedNote = repos.Count > 1
            ? $"Total run cost shared across {repos.Count} repos — Discover allocated to each repo equally; BootstrapRound calls already tagged per repo."
            : null;

        var written = 0;
        foreach (var repo in repos)
        {
            var sandbox = ResolvePerRepoSandbox(pipeline, repo);
            if (sandbox is null)
            {
                logger.LogWarning("WriteRunResult: no sandbox for repo '{Repo}' — skipping init run-doc", repo.Name);
                continue;
            }
            var reader = readerFactory.Create(sandbox);
            var repoComponents = components is not null && components.TryGetValue(repo.Name, out var rc) ? rc : null;
            var repoOutputs = outputs is not null && outputs.TryGetValue(repo.Name, out var ro) ? ro : null;
            var runDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir, RunsDir, $"{runId}-init");

            var planMd = RunResultFormatter.FormatInitPlan(runId, repo.Name, repoComponents);
            await reader.WriteAsync(Path.Combine(runDir, "plan.md"), planMd, cancellationToken);
            await WriteOptionalArtifactsAsync(reader, runDir, pipeline, cancellationToken);
            var resultMd = RunResultFormatter.FormatInitResult(
                runId, duration, cost, trail, decisions,
                dialogueEntries.Count > 0 ? dialogueEntries : null,
                perSkillBreakdown,
                repoName: repo.Name, components: repoComponents,
                bootstrapOutputsByContext: repoOutputs,
                sharedCostNote: sharedNote);
            await reader.WriteAsync(Path.Combine(runDir, "result.md"), resultMd, cancellationToken);
            await TryStoreResultAsync(runId, resultMd, cancellationToken);

            await AppendToContextYamlAsync(
                reader,
                Path.Combine(context.Repository.LocalPath, AgentSmithDir, ContextFileName),
                runId, ticket: null, cancellationToken);
            written++;
            logger.LogInformation(
                "WriteRunResult: repo {Repo} init run-doc written to {Dir}", repo.Name, Path.GetFileName(runDir));
        }

        return CommandResult.Ok(
            $"Run {RunIdGenerator.FormatForDisplay(runId)} recorded in {written} repo(s)");
    }

    private async Task<CommandResult> WriteInitSingleSandboxAsync(
        WriteRunResultContext context, string runId, CancellationToken cancellationToken)
    {
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);
        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var runDir = Path.Combine(agentDir, RunsDir, $"{runId}-init");
        await WriteOptionalArtifactsAsync(reader, runDir, context.Pipeline, cancellationToken);
        var (cost, duration) = ResolveCostAndDuration(context.Pipeline);
        var trail = TryGet<List<ExecutionTrailEntry>>(context.Pipeline, ContextKeys.ExecutionTrail);
        var decisions = TryGet<List<PlanDecision>>(context.Pipeline, ContextKeys.Decisions);
        var dialogueEntries = dialogueTrail.GetAll();
        var perSkillBreakdown = ResolvePerSkillBreakdown(context.Pipeline);
        var resultMd = RunResultFormatter.FormatInitResult(
            runId, duration, cost, trail, decisions,
            dialogueEntries.Count > 0 ? dialogueEntries : null, perSkillBreakdown);
        await reader.WriteAsync(Path.Combine(runDir, "result.md"), resultMd, cancellationToken);
        await TryStoreResultAsync(runId, resultMd, cancellationToken);
        await AppendToContextYamlAsync(
            reader, Path.Combine(agentDir, ContextFileName), runId, ticket: null, cancellationToken);
        return CommandResult.Ok($"Run {RunIdGenerator.FormatForDisplay(runId)} recorded in {Path.GetFileName(runDir)}");
    }

    private async Task TryStoreResultAsync(string runId, string resultMd, CancellationToken ct)
    {
        try
        {
            await artifactStore.WriteResultMarkdownAsync(runId, resultMd, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to cache result.md for {RunId} in artifact store — disk write + PR remain authoritative",
                runId);
        }
    }

    private static ISandbox? ResolvePerRepoSandbox(PipelineContext pipeline, RepoConnection repo)
    {
        var matches = SandboxTargets.SandboxesForRepo(pipeline, repo);
        if (matches.Count > 0) return matches[0].Value;
        return pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var legacy) ? legacy : null;
    }

    private static (RunCostSummary? Cost, int DurationSeconds) ResolveCostAndDuration(PipelineContext pipeline)
    {
        var cost = pipeline.TryGet<RunCostSummary>(ContextKeys.RunCostSummary, out var explicitCost)
            && explicitCost is not null
            ? explicitCost
            : PipelineCostTracker.GetOrCreate(pipeline).BuildSummary();
        var duration = pipeline.TryGet<int>(ContextKeys.RunDurationSeconds, out var explicitSeconds)
            && explicitSeconds > 0
            ? explicitSeconds
            : pipeline.TryGet<DateTimeOffset>(ContextKeys.RunStartedAt, out var startedAt)
                ? (int)Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalSeconds)
                : 0;
        return (cost, duration);
    }

    private static T? TryGet<T>(PipelineContext pipeline, string key) where T : class
        => pipeline.TryGet<T>(key, out var value) ? value : null;

    /// <summary>
    /// p0169a: harvest repos / repo_mode / sandbox_count / pipeline_name /
    /// status / started_at from <see cref="PipelineContext"/> so the dashboard
    /// can render topology badges without re-deriving from execution-trail.
    /// </summary>
    private static RunMetaTopology ResolveTopology(PipelineContext pipeline, string runId)
    {
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var rs) && rs is not null
            ? rs.Select(r => r.Name).ToList()
            : null;
        var repoMode = repos is { Count: > 1 } ? "multi" : "mono";

        int sandboxCount = 0;
        if (pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var sandboxes) && sandboxes is not null)
            sandboxCount = sandboxes.Count;
        else if (pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out _))
            sandboxCount = 1;

        var pipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null;
        var startedAt = pipeline.TryGet<DateTimeOffset>(ContextKeys.RunStartedAt, out var st)
            ? (DateTimeOffset?)st : null;

        return new RunMetaTopology(
            RunId: runId,
            PipelineName: pipelineName,
            Status: "done",
            StartedAt: startedAt,
            RepoMode: repoMode,
            SandboxCount: sandboxCount,
            Repos: repos);
    }

    /// <summary>
    /// p0128a: persists the structured plan/diff/bootstrap artifacts alongside the
    /// existing markdown when present. Source-of-truth for replay scenarios.
    /// </summary>
    private static async Task WriteOptionalArtifactsAsync(
        ISandboxFileReader reader, string runDir, PipelineContext pipeline, CancellationToken ct)
    {
        if (pipeline.TryGet<string>(ContextKeys.PlanJson, out var planJson) && !string.IsNullOrEmpty(planJson))
            await reader.WriteAsync(Path.Combine(runDir, "plan.json"), PrettyPrint(planJson), ct);

        if (pipeline.TryGet<string>(ContextKeys.DiffJson, out var diffJson) && !string.IsNullOrEmpty(diffJson))
            await reader.WriteAsync(Path.Combine(runDir, "diff.json"), PrettyPrint(diffJson), ct);

        if (pipeline.TryGet<string>(ContextKeys.BootstrapMarkdown, out var bootstrapMd)
            && !string.IsNullOrEmpty(bootstrapMd))
            await reader.WriteAsync(Path.Combine(runDir, "bootstrap.md"), bootstrapMd, ct);
    }

    private static string PrettyPrint(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc.RootElement, JsonOptions);
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private static IReadOnlyList<CallCostRecord>? ResolvePerSkillBreakdown(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<PipelineCostTracker>("PipelineCostTracker", out var tracker)
            || tracker is null)
            return null;
        var breakdown = tracker.PerSkillBreakdown;
        return breakdown.Count == 0 ? null : breakdown;
    }

    internal static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s]+", "-");
        slug = slug.Trim('-');
        return slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
    }

    /// <summary>
    /// Appends <c>"{runId}": "{entry}"</c> under a top-level <c>runs:</c> key,
    /// creating that key at end-of-file when absent. Fixes the previous silent
    /// no-op when the target repo's context.yaml lacked the legacy
    /// <c>state.active:</c> anchor (observed in run a6914f38).
    /// </summary>
    internal static async Task AppendToContextYamlAsync(
        ISandboxFileReader reader, string contextPath, string runId, Ticket? ticket, CancellationToken ct)
    {
        var content = await reader.TryReadAsync(contextPath, ct) ?? string.Empty;

        // p0130c-followup: init-mode runs have no ticket; render a "bootstrap"
        // entry so operators see the run history grow consistently across modes.
        var summary = ticket is not null ? FormatTicketSummary(ticket) : "bootstrap: init-project";
        var entryLine = $"  \"{runId}\": \"{summary}\"";

        var runsKey = new Regex(@"^runs:\s*$", RegexOptions.Multiline);
        if (runsKey.IsMatch(content))
        {
            content = AppendUnderRunsKey(content, entryLine);
        }
        else
        {
            if (content.Length > 0 && !content.EndsWith('\n')) content += "\n";
            if (content.Length > 0) content += "\n";
            content += "runs:\n" + entryLine + "\n";
        }

        await reader.WriteAsync(contextPath, content, ct);
    }

    private static string AppendUnderRunsKey(string content, string entryLine)
    {
        var lines = content.Split('\n').ToList();
        var runsIdx = lines.FindIndex(l => Regex.IsMatch(l, @"^runs:\s*$"));
        var insertAt = runsIdx + 1;
        while (insertAt < lines.Count && (lines[insertAt].StartsWith("  ") || lines[insertAt].Length == 0))
        {
            if (lines[insertAt].Length == 0) break;
            insertAt++;
        }
        lines.Insert(insertAt, entryLine);
        return string.Join('\n', lines);
    }

    private static string FormatTicketSummary(Ticket ticket)
    {
        var changeType = ticket.Title.StartsWith("fix", StringComparison.OrdinalIgnoreCase)
            ? "fix" : "feat";
        return $"{changeType} #{ticket.Id}: {ticket.Title}";
    }
}
