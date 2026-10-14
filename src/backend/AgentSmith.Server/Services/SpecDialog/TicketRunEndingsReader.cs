using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Why each of a ticket's runs ended: the run's summary and cancel reason, its first failed step and
/// its last sandbox that ended other than by a release. One set-based query per table over the run ids.
/// </summary>
public sealed class TicketRunEndingsReader
{
    private const string FailedStatus = "failed";

    private static readonly string[] Releases =
    [
        SandboxEndReasons.Released, SandboxEndReasons.ReleasedAtPark,
        SandboxEndReasons.ReleasedAtRequeue, SandboxEndReasons.ReleasedAtEnd,
    ];

    public async Task<IReadOnlyDictionary<string, TicketRunEnding>> ReadAsync(
        IUnitOfWork uow, List<string> ids, CancellationToken ct)
    {
        var runs = await uow.Set<Run>().AsNoTracking().Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, r.Summary, r.CancelReason }).ToListAsync(ct);
        var failed = await FirstFailedStepsAsync(uow, ids, ct);
        var ended = await LastAbnormalEndsAsync(uow, ids, ct);
        return runs.ToDictionary(r => r.Id, r => new TicketRunEnding(
            TicketRunText.Cut(r.Summary), r.CancelReason,
            failed.TryGetValue(r.Id, out var step)
                ? step with { Message = MessageBeside(step.Message, r.Summary) }
                : null,
            ended.GetValueOrDefault(r.Id)), StringComparer.Ordinal);
    }

    private static async Task<Dictionary<string, TicketRunFailedStep>> FirstFailedStepsAsync(
        IUnitOfWork uow, List<string> ids, CancellationToken ct) =>
        (await uow.Set<RunStep>().AsNoTracking()
            .Where(s => ids.Contains(s.RunId) && s.Status == FailedStatus)
            .Select(s => new { s.RunId, s.Id, s.StepIndex, s.StepName, s.ResultMessage }).ToListAsync(ct))
        .GroupBy(s => s.RunId)
        .Select(g => g.OrderBy(s => s.StepIndex).ThenBy(s => s.Id).First())
        .ToDictionary(s => s.RunId, s => new TicketRunFailedStep(s.StepIndex, s.StepName, s.ResultMessage),
            StringComparer.Ordinal);

    private static async Task<Dictionary<string, TicketRunSandboxEnd>> LastAbnormalEndsAsync(
        IUnitOfWork uow, List<string> ids, CancellationToken ct) =>
        (await uow.Set<RunSandbox>().AsNoTracking()
            .Where(s => ids.Contains(s.RunId) && s.EndReason != null && !Releases.Contains(s.EndReason))
            .Select(s => new { s.RunId, s.Id, s.EventSeq, s.EndReason, s.ExitCode, s.OomKilled }).ToListAsync(ct))
        .GroupBy(s => s.RunId)
        .Select(g => g.OrderByDescending(s => s.EventSeq).ThenByDescending(s => s.Id).First())
        .ToDictionary(s => s.RunId, s => new TicketRunSandboxEnd(s.EndReason!, s.ExitCode, s.OomKilled),
            StringComparer.Ordinal);

    // A step message repeating the summary says nothing the summary does not.
    private static string? MessageBeside(string? message, string? summary) =>
        string.IsNullOrWhiteSpace(message) || string.Equals(message.Trim(), summary?.Trim(), StringComparison.Ordinal)
            ? null
            : TicketRunText.Cut(message);
}
