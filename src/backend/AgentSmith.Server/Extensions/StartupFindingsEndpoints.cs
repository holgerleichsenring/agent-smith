using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0391a: what is wrong with this installation, over HTTP. Mapped unconditionally — it
/// is the channel a degraded server reports itself through, so it cannot sit behind the
/// dashboard gate or behind any of the dependencies it reports on. Read-only: the
/// mutation surface stays in the gated config studio.
/// </summary>
internal static class StartupFindingsEndpoints
{
    internal static WebApplication MapStartupFindingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config/findings", (IStartupFindings findings) =>
            Results.Ok(StartupFindingsResponse.From(findings.All))).Anonymous(
            "the channel that reports a broken authority cannot depend on that authority");
        return app;
    }
}
