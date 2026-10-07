using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// 2026-10-06-03c7e: records an executed spec in the repository carrying its series — the done
/// file and its companions, the index line, the manifest — in one series commit. One path for a
/// single- and a multi-sandbox run: the carrier's sandbox is resolved the same way for both, and
/// no other repository gets a file or a line.
/// </summary>
public sealed class SpecDoneRecorder(
    ISandboxFileReaderFactory readerFactory,
    SandboxTargets sandboxTargets,
    SpecDoneFiles doneFiles,
    PhaseIndexWriter indexWriter,
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
                $"Phase {record.Phase.PhaseId} ran but no sandbox of '{record.Carrier.Name}' or no ticket branch can carry its record");
        try
        {
            return await WriteAndCommitAsync(pipeline, target, record, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Recording {PhaseId} as executed failed", record.Phase.PhaseId);
            return CommandResult.Fail(
                $"Phase {record.Phase.PhaseId} ran but its record was not committed: {ex.Message.Split('\n', 2)[0].Trim()}");
        }
    }

    private async Task<CommandResult> WriteAndCommitAsync(
        PipelineContext pipeline, SeriesRecordTarget target, SpecDoneRecord record, CancellationToken ct)
    {
        var files = readerFactory.Create(target.Sandbox);
        var move = await doneFiles.WriteAsync(files, record.Phase, record.Body, ct);
        var index = await indexWriter.WriteAsync(pipeline, target.Sandbox, target.SandboxKey,
            Repository.SandboxWorkPath, record.Phase.PhaseId, record.IndexLine, ct);
        var executed = record.Set with { Executed = [.. record.Set.Executed, record.Phase.PhaseId] };
        var manifest = seriesFiles.Manifest(executed);
        await files.WriteAsync(manifest.Path, manifest.Content, ct);
        var staged = new List<string>(move.Written) { manifest.Path };
        if (index is not null) staged.Add(index);
        var sha = await commit.CommitAsync(target, record, executed, staged, move.Planned, ct);
        pipeline.Set(ContextKeys.SpecSet, executed);
        logger.LogInformation("Phase {PhaseId} recorded in {Path} as {Sha}", record.Phase.PhaseId, move.Written[0], sha);
        return CommandResult.Ok($"Phase record {move.Written[0]} committed as {sha}"
            + (index is null ? " (no index line: no discovered context)" : $", indexed in {index}"));
    }

    private SeriesRecordTarget? Target(PipelineContext pipeline, SpecDoneRecord record)
    {
        var branch = pipeline.TryGet<Repository>(ContextKeys.Repository, out var r) ? r?.CurrentBranch.Value : null;
        if (string.IsNullOrWhiteSpace(branch)) return null;
        var project = pipeline.TryGet<string>(ContextKeys.ProjectName, out var name) ? name ?? string.Empty : string.Empty;
        var matches = sandboxTargets.SandboxesForRepo(pipeline, record.Carrier);
        if (matches.Count > 0) return new SeriesRecordTarget(matches[0].Value, matches[0].Key, branch!, project);
        return pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var single) && single is not null
            ? new SeriesRecordTarget(single, null, branch!, project)
            : null;
    }
}
