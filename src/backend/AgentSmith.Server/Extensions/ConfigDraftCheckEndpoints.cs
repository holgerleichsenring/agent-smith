using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;
using Microsoft.AspNetCore.Mvc;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-02-5f89b: the studio's Test action — an unsaved connection or tracker checked against
/// its host, step by step. Both permissions, together: the draft names any saved secret and any
/// host, so with diagnostics.probe alone an operator could send an admin-held token to a host of
/// their choosing; whoever may write the entry may test it. The answer is our steps — no token,
/// no response body.
/// </summary>
internal static class ConfigDraftCheckEndpoints
{
    internal static WebApplication MapConfigDraftCheckEndpoints(this WebApplication app)
    {
        app.MapPost("/api/config/connections/check",
            async ([FromBody] ConnectionEntity draft, [FromServices] IConnectionDraftCheckRunner runner,
                CancellationToken ct) => Results.Ok(await runner.RunAsync(draft, ct)))
           .Needs(Permissions.ConfigWrite, Permissions.DiagnosticsProbe);

        app.MapPost("/api/config/trackers/check",
            async ([FromBody] TrackerEntity draft, [FromServices] ITrackerDraftCheckRunner runner,
                CancellationToken ct) => Results.Ok(await runner.RunAsync(draft, ct)))
           .Needs(Permissions.ConfigWrite, Permissions.DiagnosticsProbe);

        // 2026-11-23-933de: a PoC target's registry block, outside the PoCs switch, because a target is set up before it.
        app.MapPost("/api/config/pocs/registry/check",
            async ([FromBody] PocRegistryConfig draft, [FromServices] IPocRegistryDraftCheck check,
                CancellationToken ct) => Results.Ok(await check.RunAsync(draft, ct)))
           .Needs(Permissions.ConfigWrite, Permissions.DiagnosticsProbe);

        // 2026-11-23-c81ec: a PoC target's Argo CD block — its token's expiry, the probe and the ApplicationSet's state.
        app.MapPost("/api/config/pocs/argo/check",
            async ([FromBody] PocArgoCheckRequest draft, [FromServices] IPocArgoDraftCheck check,
                CancellationToken ct) => Results.Ok(await check.RunAsync(draft, ct)))
           .Needs(Permissions.ConfigWrite, Permissions.DiagnosticsProbe);

        // 2026-11-23-933dg: a PoC target's Sealed Secrets certificate, read from a pasted PEM or fetched from an https URL —
        // public material, a run-free read that saves nothing, outside the PoCs switch like the registry check.
        app.MapPost("/api/config/pocs/sealed-secrets-cert/fetch",
            async ([FromBody] Services.Pocs.Targets.SealingCertificateRequest request,
                [FromServices] Services.Pocs.Targets.SealingCertificateFetch fetch, CancellationToken ct) =>
            {
                var (answer, refusal) = await fetch.ReadAsync(request, ct);
                return answer is not null ? Results.Ok(answer) : refusal!.Answer();
            })
           .Needs(Permissions.ConfigWrite);

        return app;
    }
}
