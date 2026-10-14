using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0380: loads the experiential-memory INDEX of each repository at plan time, mirroring
/// LoadCodingPrinciples. An absent store publishes an empty index — the cheap pointer layer, never
/// an error. The read itself is <see cref="MemoryIndexReader"/>'s.
/// </summary>
public sealed class LoadMemoryIndexHandler(MemoryIndexReader reader) : ICommandHandler<LoadMemoryIndexContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadMemoryIndexContext context, CancellationToken cancellationToken)
    {
        var read = await reader.RefreshAsync(context.Pipeline, cancellationToken);
        if (read.Repositories == 0) return CommandResult.Ok("No Sandboxes in pipeline context, skipping");
        return CommandResult.Ok(read.Loaded > 0
            ? $"Loaded memory index from {read.Loaded} of {read.Repositories} repositor(ies)"
            : "No memory store present — empty index");
    }
}
