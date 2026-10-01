using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: the process-wide services one render_reference call composes — parsing the
/// source, the server's egress pre-check, the render itself, the image deposit and the text.
/// Grouped so a per-turn tool host is built from one injected value and the turn's own facts.
/// </summary>
public sealed record RenderReferenceServices(
    RenderSourceParser Sources,
    RenderUrlGuard Guard,
    ReferenceRenderer Renderer,
    IToolImageDeposit Images,
    RenderResultText Text);
