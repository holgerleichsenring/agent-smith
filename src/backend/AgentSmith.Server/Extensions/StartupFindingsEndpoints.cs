using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Startup;
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
        app.MapGet("/api/config/findings", Read).Anonymous(
            "the channel that reports a broken authority cannot depend on that authority");
        return app;
    }

    /// <summary>
    /// 2026-10-15-4a4dd: a finding naming a project is diagnosing configuration — config.read business. An enforcing
    /// installation drops it for every other caller, anonymous included, before the counts are taken.
    /// </summary>
    internal static async Task<IResult> Read(
        [FromServices] IStartupFindings findings, [FromServices] IBuildMismatchDetector builds, [FromQuery] string? build,
        ClaimsPrincipal user, [FromServices] ProjectAccess access, [FromServices] StartupProbePass? pass,
        CancellationToken cancellationToken)
    {
        // 2026-10-29-7f2d: the probes run after the bind; an early read waits for their first pass.
        if (pass is not null) await pass.WaitAsync(StartupProbePass.ReadBudget, cancellationToken);
        IReadOnlyList<StartupFinding> all = [.. findings.All, .. builds.Compare(build)];
        var hidesProjects = access.Enforcing && !access.HoldsEverywhere(user, [Permissions.ConfigRead]);
        return Results.Ok(StartupFindingsResponse.From(
            hidesProjects ? [.. all.Where(f => string.IsNullOrWhiteSpace(f.Project))] : all));
    }
}
