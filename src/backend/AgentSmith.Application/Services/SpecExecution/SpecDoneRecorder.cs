using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-10-06-03c7e: records an executed spec in the repository carrying its series — the done
/// file and its companions, the index line, the manifest — in one series commit. One path for a
/// single- and a multi-sandbox run: the carrier's sandbox is resolved the same way for both, and
/// no other repository gets a file or a line.
/// <para>
/// 2026-10-12-f5f7d: that repository is the spec's OWN, and the manifest rewritten is its
/// sub-series' — its specs, the publish stamp, the member list. A run of several repositories never
/// falls back to the run-wide sandbox: that would write the record into a repository it does not
/// belong to.
/// </para>
/// </summary>
public sealed class SpecDoneRecorder(
    ISandboxFileReaderFactory readerFactory,
    SandboxTargets sandboxTargets,
    SpecDoneFiles doneFiles,
    SpecIndexWriter indexWriter,
    SeriesFiles seriesFiles,
    SeriesRecordCommit commit,
    ILogger<SpecDoneRecorder> logger)
{
    public async Task<CommandResult> RecordAsync(
        PipelineContext pipeline, SpecDoneRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(record);
        var target = Target(pipeline, record);
        if (target is null)
            return CommandResult.Fail(
                $"Spec {record.Spec.SpecId} ran but no sandbox of '{record.Carrier.Name}' or no ticket branch can carry its record");
        try
        {
            return await WriteAndCommitAsync(pipeline, target, record, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Recording {SpecId} as executed failed", record.Spec.SpecId);
            return CommandResult.Fail(
                $"Spec {record.Spec.SpecId} ran but its record was not committed: {ex.Message.Split('\n', 2)[0].Trim()}");
        }
    }

    private async Task<CommandResult> WriteAndCommitAsync(
        PipelineContext pipeline, SeriesRecordTarget target, SpecDoneRecord record, CancellationToken ct)
    {
        var files = readerFactory.Create(target.Sandbox);
        var move = await doneFiles.WriteAsync(files, record.Spec, record.Body, ct);
        var index = await indexWriter.WriteAsync(pipeline, target.Sandbox, target.SandboxKey,
            Repository.SandboxWorkPath, record.Spec.SpecId, record.IndexLine, ct);
        var executed = record.Set with { Executed = [.. record.Set.Executed, record.Spec.SpecId] };
        var manifest = seriesFiles.Manifest(executed.OfRepository(record.Carrier.Name));
        await files.WriteAsync(manifest.Path, manifest.Content, ct);
        var staged = new List<string>(move.Written) { manifest.Path };
        if (index is not null) staged.Add(index);
        var sha = await commit.CommitAsync(target, record, executed, staged, move.Planned, ct);
        pipeline.Set(ContextKeys.SpecSet, executed);
        logger.LogInformation("Spec {SpecId} recorded in {Path} as {Sha}", record.Spec.SpecId, move.Written[0], sha);
        return CommandResult.Ok($"Spec record {move.Written[0]} committed as {sha}"
            + (index is null ? " (no index line: no discovered context)" : $", indexed in {index}"));
    }

    private SeriesRecordTarget? Target(PipelineContext pipeline, SpecDoneRecord record)
    {
        var branch = pipeline.TryGet<Repository>(ContextKeys.Repository, out var r) ? r?.CurrentBranch.Value : null;
        if (string.IsNullOrWhiteSpace(branch)) return null;
        var project = pipeline.TryGet<string>(ContextKeys.ProjectName, out var name) ? name ?? string.Empty : string.Empty;
        var matches = sandboxTargets.SandboxesForRepo(pipeline, record.Carrier);
        if (matches.Count > 0) return new SeriesRecordTarget(matches[0].Value, matches[0].Key, branch!, project);
        return !record.MultiRepo && pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var single) && single is not null
            ? new SeriesRecordTarget(single, null, branch!, project)
            : null;
    }
}
