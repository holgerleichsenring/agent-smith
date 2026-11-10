using System.Threading.Channels;
using AgentSmith.Contracts.Prompts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-09-0620a: stores what a model call was sent. Blocks are written once; a call that extends a recent record of
/// its run stores only the blocks it appended.
/// <para>
/// 2026-11-09-0620l: off the call path. The id is a hash the call computes at once; the rows are written by one
/// background writer in the order calls were made, so a parent is always stored before its child and a slow database
/// never delays a model call. A full queue drops the write — the call keeps its id and the viewer finds nothing.
/// </para>
/// </summary>
public sealed class PromptRecorder : IPromptRecorder
{
    private const int QueueCapacity = 1024;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PromptRecentRecords _recent;
    private readonly ILogger<PromptRecorder> _logger;
    private readonly Channel<PromptWrite> _queue =
        Channel.CreateBounded<PromptWrite>(new BoundedChannelOptions(QueueCapacity) { SingleReader = true });
    private Task _lastWrite = Task.CompletedTask;

    public PromptRecorder(IServiceScopeFactory scopeFactory, PromptRecentRecords recent, ILogger<PromptRecorder> logger)
    {
        _scopeFactory = scopeFactory;
        _recent = recent;
        _logger = logger;
        _ = Task.Run(WriteLoopAsync);
    }

    public Task<string?> RecordAsync(
        string runId, IReadOnlyList<PromptBlock> blocks, int toolCount, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(runId) || blocks.Count == 0) return Task.FromResult<string?>(null);
        var hashes = blocks.Select(PromptHashes.Of).ToList();
        var id = PromptHashes.Record(runId, hashes);
        var parent = _recent.LongestPrefixOf(runId, hashes);
        _recent.Remember(runId, id, hashes);
        var write = new PromptWrite(runId, id, parent?.Id, parent?.Length ?? 0, blocks, hashes, toolCount);
        if (!_queue.Writer.TryWrite(write))
        {
            _logger.LogWarning("Prompt store queue is full; the prompt of a call of run {RunId} is not kept.", runId);
            return Task.FromResult<string?>(id);
        }
        lock (_queue) _lastWrite = write.Done.Task;
        return Task.FromResult<string?>(id);
    }

    /// <summary>Completes once every write queued so far has been attempted — for tests and orderly shutdown.</summary>
    public Task DrainAsync()
    {
        lock (_queue) return _lastWrite;
    }

    private async Task WriteLoopAsync()
    {
        await foreach (var write in _queue.Reader.ReadAllAsync())
        {
            try
            {
                await new PromptRecordWriter(_scopeFactory).StoreAsync(write, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not store the prompt of a model call of run {RunId}.", write.RunId);
            }
            finally
            {
                write.Done.TrySetResult();
            }
        }
    }
}

/// <summary>2026-11-09-0620l: one call's prompt, waiting for the background writer.</summary>
public sealed record PromptWrite(
    string RunId, string Id, string? ParentId, int ParentLength,
    IReadOnlyList<PromptBlock> Blocks, IReadOnlyList<string> Hashes, int ToolCount)
{
    public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
