using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Maps a typed RunEvent onto its ER entity (the run-level row + its children).
/// Pure projection — no buffering, no Redis. The raw-event trail is the
/// projector's concern; this applies only the events that carry structured run
/// facts the dashboard reads (status, steps, cost, repos, sandboxes, decisions).
/// </summary>
public sealed class RunEventApplier
{
    public async Task ApplyAsync(
        AgentSmithDbContext ctx, AgentSmith.Contracts.Events.RunEvent ev, CancellationToken ct)
    {
        switch (ev)
        {
            case RunStartedEvent e: await StartRunAsync(ctx, e, ct); break;
            case TicketFetchedEvent e: await UpdateRunAsync(ctx, e.RunId, r => r.TicketTitle = e.Title, ct); break;
            case RunFinishedEvent e: await FinishRunAsync(ctx, e, ct); break;
            case StepStartedEvent e: ctx.RunSteps.Add(StepFrom(e)); await ctx.SaveChangesAsync(ct); break;
            case StepFinishedEvent e: await FinishStepAsync(ctx, e, ct); break;
            case LlmCallFinishedEvent e: ctx.RunLlmCalls.Add(LlmFrom(e)); await ctx.SaveChangesAsync(ct); break;
            case SandboxCreatedEvent e: ctx.RunSandboxes.Add(SandboxFrom(e)); await ctx.SaveChangesAsync(ct); break;
            case SandboxDisposedEvent e: await DisposeSandboxAsync(ctx, e, ct); break;
            case DecisionLoggedEvent e: ctx.RunDecisions.Add(DecisionFrom(e)); await ctx.SaveChangesAsync(ct); break;
            case PullRequestOutcomeEvent e: await UpsertRepoAsync(ctx, e, ct); break;
            default: break; // trail-only event — the projector still persists the raw row
        }
    }

    private static async Task StartRunAsync(AgentSmithDbContext ctx, RunStartedEvent e, CancellationToken ct)
    {
        if (await ctx.Runs.AnyAsync(r => r.Id == e.RunId, ct)) return;
        ctx.Runs.Add(new Run
        {
            Id = e.RunId, Pipeline = e.Pipeline, Trigger = e.Trigger, Status = "running",
            TicketId = e.TicketId ?? string.Empty, AgentName = e.AgentName, StartedAt = e.StartedAt,
        });
        foreach (var repo in e.Repos)
            ctx.RunRepos.Add(new RunRepo { RunId = e.RunId, RepoName = repo });
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task FinishRunAsync(AgentSmithDbContext ctx, RunFinishedEvent e, CancellationToken ct) =>
        await UpdateRunAsync(ctx, e.RunId, r =>
        {
            r.Status = e.Status;
            r.FinishedAt = e.Timestamp;
            r.Summary = e.Summary;
            if (e.CostUsd is { } cost) r.CostTotalUsd = cost;
        }, ct);

    private static async Task UpdateRunAsync(
        AgentSmithDbContext ctx, string runId, Action<Run> mutate, CancellationToken ct)
    {
        var run = await ctx.Runs.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        mutate(run);
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task FinishStepAsync(AgentSmithDbContext ctx, StepFinishedEvent e, CancellationToken ct)
    {
        var step = await ctx.RunSteps
            .Where(s => s.RunId == e.RunId && s.StepIndex == e.StepIndex)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (step is null) { ctx.RunSteps.Add(StepFrom(e)); }
        else { step.Status = e.Status; step.DurationSeconds = e.DurationMs / 1000.0; step.ResultMessage = e.Reason; }
        await ctx.SaveChangesAsync(ct);
    }

    private static async Task DisposeSandboxAsync(AgentSmithDbContext ctx, SandboxDisposedEvent e, CancellationToken ct)
    {
        var box = await ctx.RunSandboxes
            .Where(s => s.RunId == e.RunId && s.RepoName == e.Repo)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (box is not null) { box.Status = e.ExitCode == 0 ? "ok" : "failed"; await ctx.SaveChangesAsync(ct); }
    }

    private static async Task UpsertRepoAsync(AgentSmithDbContext ctx, PullRequestOutcomeEvent e, CancellationToken ct)
    {
        var repo = await ctx.RunRepos.FirstOrDefaultAsync(r => r.RunId == e.RunId && r.RepoName == e.Repo, ct)
            ?? ctx.RunRepos.Add(new RunRepo { RunId = e.RunId, RepoName = e.Repo }).Entity;
        repo.PrUrl = e.Url; repo.PrStatus = e.Status; repo.Reason = e.Reason;
        await ctx.SaveChangesAsync(ct);
    }

    private static RunStep StepFrom(StepStartedEvent e) =>
        new() { RunId = e.RunId, StepIndex = e.StepIndex, StepName = e.StepName, DisplayName = e.DisplayName, Status = "running" };

    private static RunStep StepFrom(StepFinishedEvent e) =>
        new() { RunId = e.RunId, StepIndex = e.StepIndex, StepName = e.Status, Status = e.Status, DurationSeconds = e.DurationMs / 1000.0, ResultMessage = e.Reason };

    private static RunLlmCall LlmFrom(LlmCallFinishedEvent e) =>
        new() { RunId = e.RunId, Role = e.Role, Phase = e.Phase, Model = e.Model, TokensIn = e.TokensIn, TokensOut = e.TokensOut, CostUsd = e.CostUsd, DurationMs = e.DurationMs };

    private static RunSandbox SandboxFrom(SandboxCreatedEvent e) =>
        new() { RunId = e.RunId, Key = e.Repo, RepoName = e.Repo, ToolchainImage = e.Image, Status = "created" };

    private static RunDecision DecisionFrom(DecisionLoggedEvent e) =>
        new() { RunId = e.RunId, Name = e.Chose, Reason = e.Reason };
}
