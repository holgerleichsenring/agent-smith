using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// What the master's prompt reads back out of the pipeline about the RUN — the typed lists
/// FetchTicket published, what the framework staged, and which attachments are left over
/// once the viewable images and the materialized documents are accounted for.
/// <para>
/// 2026-09-13-6f35 lifted these out of AgenticMasterHandler, which is at its length ratchet
/// and had to make room for the template wiring. They are pure functions over values, so
/// they stay static: the extraction is about where a responsibility lives, not about DI.
/// </para>
/// </summary>
internal static class MasterPipelineFacts
{
    // p0422: what the framework staged for this run, so the master states it rather than
    // theorising about it — run 22 wrote "no credentials in sandbox" without ever trying.
    internal static IReadOnlyList<string>? StagedRegistries(PipelineContext pipeline) =>
        pipeline.TryGet<List<string>>(ContextKeys.StagedRegistries, out var staged) ? staged : null;

    // Everything that is neither a viewable image nor a materialized document is
    // listed by name + size only — never downloaded, never inlined.
    internal static List<AttachmentRef> OtherBinaries(
        IReadOnlyList<AttachmentRef> refs, IReadOnlyList<MaterializedTicketDocument> materialized)
    {
        var origins = materialized
            .Select(m => m.OriginFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return refs
            .Where(r => !TicketImageAttachment.IsSupportedImage(r) && !origins.Contains(r.FileName))
            .ToList();
    }

    internal static IReadOnlyList<T> ListFrom<T>(PipelineContext pipeline, string key) =>
        pipeline.TryGet<IReadOnlyList<T>>(key, out var value) && value is not null ? value : [];
}
