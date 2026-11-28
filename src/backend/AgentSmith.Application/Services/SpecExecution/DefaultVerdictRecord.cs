using System.Globalization;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-10-13-8762p: the record step's copy of the ticket's decided defaults into the ticket branch, so deleting
/// the run that took one loses no verdict an attempt has since recorded. A row naming a spec of the set placed in
/// the recorded spec's repository goes into that spec's decision file; a row naming no spec of the set into the
/// set's first spec's decision file, in that spec's repository (2026-11-28-db88d). Each is written once: the file's marker is checked in the same
/// read-append-write. A failed read or write is logged — the spec's record never fails for its copy.
/// </summary>
public sealed class DefaultVerdictRecord(
    IDecidedDefaults decided, IDefaultVerdictFile file, ILogger<DefaultVerdictRecord> logger)
{
    /// <summary>The repository paths written, for the record's staged list.</summary>
    public async Task<IReadOnlyList<string>> WriteAsync(
        PipelineContext pipeline, ISandboxFileReader files, SpecDoneRecord record, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.ProjectName, out var project) || string.IsNullOrWhiteSpace(project)
            || !pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticket) || ticket is null) return [];
        try
        {
            var rows = await decided.ReadAsync(project!, ticket.Value, record.Set.Approval?.Conversation, ct);
            var placed = (record.MultiRepo ? record.Set.OfRepository(record.Carrier.Name) : record.Set)
                .Specs.Select(s => s.SpecId).ToHashSet(StringComparer.Ordinal);
            var ofTheSet = record.Set.Specs.Select(s => s.SpecId).ToHashSet(StringComparer.Ordinal);
            // 2026-11-28-db88d: the set's first spec that was not retired — a retired one never runs again.
            var retired = record.Set.Retired();
            var lead = record.Set.Specs.FirstOrDefault(s => !retired.Contains(s.SpecId)) ?? record.Spec;
            var home = !record.MultiRepo || lead.Draft.Repos is [var first]
                && string.Equals(first, record.Carrier.Name, StringComparison.OrdinalIgnoreCase);
            var written = new List<string>();
            foreach (var row in rows)
                if (FileOf(row, placed, ofTheSet, home, lead.SpecId) is { } fileId
                    && await file.AppendAsync(files, fileId, row.Marker, Entry(row), ct) is { } path
                    && !written.Contains(path, StringComparer.Ordinal))
                    written.Add(path);
            return written;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The decided defaults of {Project}/#{Ticket} were not copied into the record", project, ticket.Value);
            return [];
        }
    }

    // A spec of the set is recorded in its own repository only; any other row once, in the set's first spec's file
    // (2026-11-28-db88d: never a file named after the run that decided it).
    private static string? FileOf(DecidedDefault row, ISet<string> placed, ISet<string> ofTheSet, bool home, string firstSpec) =>
        row.SpecId is { } id && ofTheSet.Contains(id) ? placed.Contains(id) ? id : null : home ? firstSpec : null;

    public static string Entry(DecidedDefault row) =>
        $"{row.Marker} {row.Verdict} by {row.By} at {row.At.ToString("u", CultureInfo.InvariantCulture)}: "
        + $"{row.Question} -> {row.Chosen}; note: {(string.IsNullOrWhiteSpace(row.Note) ? "(none)" : row.Note)}";
}
