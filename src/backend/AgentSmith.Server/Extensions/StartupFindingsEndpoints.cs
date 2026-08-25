using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0391a: what is wrong with this installation, over HTTP. Mapped unconditionally — it
/// is the channel a degraded server reports itself through, so it cannot sit behind the
/// dashboard gate or behind any of the dependencies it reports on. Read-only: the
/// mutation surface stays in the gated config studio.
/// <para>
/// 2026-08-25-8c97: a caller may name the build it came from in <c>?build=</c>. That makes
/// the difference between the two halves a finding on the channel an operator already
/// watches, at the cost of one comparison — and it reaches a browser, which cannot set a
/// request header on the hub's websocket and would have needed every call site changed to
/// set one anywhere else. The answer is the same 200 either way.
/// </para>
/// </summary>
internal static class StartupFindingsEndpoints
{
    internal static WebApplication MapStartupFindingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config/findings",
            ([FromServices] IStartupFindings findings,
                [FromServices] IBuildMismatchDetector builds,
                [FromQuery] string? build) =>
                Results.Ok(StartupFindingsResponse.From(
                    [.. findings.All, .. builds.Compare(build)]))).Anonymous(
            "the channel that reports a broken authority cannot depend on that authority");
        return app;
    }
}
