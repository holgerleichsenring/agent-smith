using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Writes run artifacts (plan.md + result.md) to .agentsmith/runs/r{NN}-{slug}/
/// and appends the run entry to state.done in .agentsmith/context.yaml.
/// Formatting is delegated to RunResultFormatter.
/// </summary>
public sealed class WriteRunResultHandler(
    ISandboxFileReaderFactory readerFactory,
    IDialogueTrail dialogueTrail,
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
        var sandbox = context.Pipeline.Get<ISandbox>(ContextKeys.Sandbox);
        var reader = readerFactory.Create(sandbox);

        var agentDir = Path.Combine(context.Repository.LocalPath, AgentSmithDir);
        var runsDir = Path.Combine(agentDir, RunsDir);

        var contextPath = Path.Combine(agentDir, ContextFileName);
        var nextRunNumber = await DetermineNextRunNumberAsync(reader, contextPath, cancellationToken);

        // p0130c-followup: init-project routes through this handler but has
        // no Ticket/Plan/Changes — use a static "init" slug + skip plan.md
        // (no plan to render) + render result.md in init-mode.
        var slug = context.Ticket is not null ? GenerateSlug(context.Ticket.Title) : "init";
        var runDirName = $"r{nextRunNumber:D2}-{slug}";
        var runDir = Path.Combine(runsDir, runDirName);

        if (context.Ticket is not null && context.Plan is not null)
        {
            var planMd = RunResultFormatter.FormatPlan(context.Ticket, context.Plan);
            await reader.WriteAsync(Path.Combine(runDir, "plan.md"), planMd, cancellationToken);
        }

        await WriteOptionalArtifactsAsync(reader, runDir, context.Pipeline, cancellationToken);

        context.Pipeline.TryGet<RunCostSummary>(ContextKeys.RunCostSummary, out var costSummary);
        context.Pipeline.TryGet<int>(ContextKeys.RunDurationSeconds, out var durationSeconds);
        context.Pipeline.TryGet<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail, out var trail);
        context.Pipeline.TryGet<List<PlanDecision>>(ContextKeys.Decisions, out var decisions);
        context.Pipeline.TryGet<SecurityTrend>(ContextKeys.SecurityTrend, out var securityTrend);
        var dialogueEntries = dialogueTrail.GetAll();
        var perSkillBreakdown = ResolvePerSkillBreakdown(context.Pipeline);

        var resultMd = context.Ticket is not null && context.Plan is not null
            ? RunResultFormatter.FormatResult(
                context.Ticket, context.Plan, context.Changes,
                nextRunNumber, durationSeconds, costSummary, trail, decisions, securityTrend,
                dialogueEntries.Count > 0 ? dialogueEntries : null,
                perSkillBreakdown)
            : RunResultFormatter.FormatInitResult(
                nextRunNumber, durationSeconds, costSummary, trail, decisions,
                dialogueEntries.Count > 0 ? dialogueEntries : null,
                perSkillBreakdown);
        await reader.WriteAsync(Path.Combine(runDir, "result.md"), resultMd, cancellationToken);

        await AppendToContextYamlAsync(reader, contextPath, nextRunNumber, context.Ticket, cancellationToken);

        context.Pipeline.Set(ContextKeys.RunNumber, nextRunNumber);

        logger.LogInformation(
            "Written run result r{RunNumber:D2} to {Dir}",
            nextRunNumber, runDirName);

        return CommandResult.Ok($"Run r{nextRunNumber:D2} recorded in {runDirName}");
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

    internal static async Task<int> DetermineNextRunNumberAsync(
        ISandboxFileReader reader, string contextPath, CancellationToken cancellationToken)
    {
        var content = await reader.TryReadAsync(contextPath, cancellationToken);
        if (content is null) return 1;

        try
        {
            var matches = Regex.Matches(content, @"^\s+r(\d+):", RegexOptions.Multiline);
            var maxNumber = 0;
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out var num) && num > maxNumber)
                    maxNumber = num;
            }
            return maxNumber + 1;
        }
        catch
        {
            return 1;
        }
    }

    internal static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s]+", "-");
        slug = slug.Trim('-');
        return slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
    }

    private static async Task AppendToContextYamlAsync(
        ISandboxFileReader reader, string contextPath, int runNumber, Ticket? ticket, CancellationToken ct)
    {
        var content = await reader.TryReadAsync(contextPath, ct);
        if (content is null) return;

        // p0130c-followup: init-mode runs have no ticket; render a "bootstrap"
        // entry so operators see the run history grow consistently across modes.
        var entry = ticket is not null
            ? FormatTicketEntry(runNumber, ticket)
            : $"    r{runNumber:D2}: \"bootstrap: init-project\"";

        var insertPattern = new Regex(@"^(\s+active:)", RegexOptions.Multiline);
        var match = insertPattern.Match(content);

        if (match.Success)
            content = content.Insert(match.Index, entry + "\n");

        await reader.WriteAsync(contextPath, content, ct);
    }

    private static string FormatTicketEntry(int runNumber, Ticket ticket)
    {
        var changeType = ticket.Title.StartsWith("fix", StringComparison.OrdinalIgnoreCase)
            ? "fix" : "feat";
        return $"    r{runNumber:D2}: \"{changeType} #{ticket.Id}: {ticket.Title}\"";
    }
}
