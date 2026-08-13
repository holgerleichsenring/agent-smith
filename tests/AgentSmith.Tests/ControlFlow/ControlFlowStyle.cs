namespace AgentSmith.Tests.ControlFlow;

/// <summary>
/// p0408: the diagram's stylesheet, in the visual language the site's existing drawings
/// already speak — the DESIGN.md palette as literal values (a static SVG cannot read the
/// page's custom properties), Inter for prose, IBM Plex Mono for anything the code owns.
/// </summary>
internal static class ControlFlowStyle
{
    public const string Block =
        """
          <style>
            .cf-bg        { fill: #fffefb; }
            .cf-surface   { fill: #f8f4f0; stroke: #c5c0b1; stroke-width: 0.6; }
            .cf-gate      { fill: #eef2f8; stroke: #b7c1d1; stroke-width: 0.6; }
            .cf-model     { fill: #f3faf4; stroke: #22c55e; stroke-width: 1.1; }
            .cf-block     { fill: none;    stroke: #22c55e; stroke-width: 1.2; stroke-dasharray: 6 5; }
            .cf-blockline { stroke: #22c55e; stroke-width: 1.2; }
            .cf-flow      { stroke: #b0a99a; stroke-width: 1.2; }
            .cf-rule      { stroke: #e6e2d8; stroke-width: 1; }
            .cf-text      { fill: #201515; font-family: 'Inter', system-ui, sans-serif; }
            .cf-mute      { fill: #8f8c80; font-family: 'Inter', system-ui, sans-serif; }
            .cf-mono      { fill: #201515; font-family: 'IBM Plex Mono', ui-monospace, monospace; }
            .cf-mono-mute { fill: #8f8c80; font-family: 'IBM Plex Mono', ui-monospace, monospace; }
            .cf-accent    { fill: #16a34a; font-family: 'IBM Plex Mono', ui-monospace, monospace; }
            .cf-tag       { fill: #16a34a; font-family: 'IBM Plex Mono', ui-monospace, monospace; letter-spacing: 0.8px; }
          </style>
        """;
}
