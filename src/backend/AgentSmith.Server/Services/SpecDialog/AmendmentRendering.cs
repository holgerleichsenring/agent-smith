using AgentSmith.Application.Services.SpecDialog;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-06-03c7c: what turns an approved proposal into the region an amendment writes — the
/// orderer, the re-id onto the ticket's series, and the filing's own renderer, so an amended
/// ticket and a freshly filed one are the same body.
/// </summary>
internal sealed record AmendmentRendering(
    PhaseTicketRenderer Renderer, EpicChildOrderer Orderer, FiledSeriesFactory Series);
