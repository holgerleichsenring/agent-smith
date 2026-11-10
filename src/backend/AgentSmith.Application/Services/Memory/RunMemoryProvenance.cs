using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-11-10-4778 review: where a coding run's '@project' memory came from — the run, its spec, and as author the
/// coding master with the pipeline it ran and the ticket that triggered it.
/// </summary>
public static class RunMemoryProvenance
{
    private const int AuthorMax = 191; // the column's indexed-string bound

    public static MemoryProvenance Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return new MemoryProvenance(
            ProjectMemorySourceKinds.Run,
            pipeline.TryGet<string>(ContextKeys.RunId, out var run) ? run ?? string.Empty : string.Empty,
            pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var spec) ? spec?.SpecId : null,
            Author(pipeline));
    }

    public static string Author(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var parts = new List<string>();
        if (pipeline.TryGet<string>(ContextKeys.PipelineName, out var name) && !string.IsNullOrWhiteSpace(name))
            parts.Add($"pipeline {name}");
        if (pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticket) && !string.IsNullOrWhiteSpace(ticket?.Value))
            parts.Add($"ticket {ticket.Value}");
        var author = parts.Count == 0 ? "coding master" : $"coding master ({string.Join(", ", parts)})";
        return author.Length <= AuthorMax ? author : author[..AuthorMax];
    }
}
