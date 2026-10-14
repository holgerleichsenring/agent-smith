using System.Security.Claims;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-8762r: a design conversation's mock versions, addressed by session id, spec id and
/// version — the screenshot the transcript draws, and the whole page to open. Each answers only a
/// caller who may reach the conversation; anything else, or a version not held, is not found.
/// <para>
/// THE PAGE IS NEVER SERVED AS A PAGE. Opened from this origin the model's HTML would run as the
/// dashboard, so it goes out as an octet-stream attachment, never sniffed, under a sandboxing
/// policy — as an uploaded .html does (<see cref="SpecDialogReferenceFileEndpoints"/>).
/// </para>
/// </summary>
internal static class SpecDialogMockEndpoints
{
    private const string Sandboxed = "sandbox";
    private const string Version = "/api/spec-dialog/conversations/{sessionId}/mocks/{specId}/{version:int}";

    internal static WebApplication MapSpecDialogMockEndpoints(this WebApplication app)
    {
        app.MapGet($"{Version}/shot", (Delegate)ShotAsync).Needs(Security.Permissions.DialogWrite);
        app.MapGet($"{Version}/html", (Delegate)HtmlAsync).Needs(Security.Permissions.DialogWrite);
        return app;
    }

    internal static async Task<IResult> ShotAsync(HttpContext http, string sessionId, string specId, int version,
        SpecDialogOwnership ownership, IConversationMockReader mocks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (!await ReachesAsync(http.User, sessionId, ownership, cancellationToken)) return Results.NotFound();
        var shot = await mocks.ShotAsync(sessionId, specId, version, cancellationToken);
        if (shot is null) return Results.NotFound();
        Harden(http);
        return Results.File(shot, "image/jpeg");
    }

    internal static async Task<IResult> HtmlAsync(HttpContext http, string sessionId, string specId, int version,
        SpecDialogOwnership ownership, IConversationMockReader mocks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (!await ReachesAsync(http.User, sessionId, ownership, cancellationToken)) return Results.NotFound();
        var page = await mocks.HtmlAsync(sessionId, specId, version, cancellationToken);
        if (page is null) return Results.NotFound();
        Harden(http);
        return Results.File(System.Text.Encoding.UTF8.GetBytes(page.Html), "application/octet-stream", $"{page.Version.Name}.html");
    }

    private static async Task<bool> ReachesAsync(
        ClaimsPrincipal user, string sessionId, SpecDialogOwnership ownership, CancellationToken ct) =>
        await ownership.MayReachSessionAsync(sessionId, ownership.OwnerOf(user), ct);

    private static void Harden(HttpContext http)
    {
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.ContentSecurityPolicy = Sandboxed;
    }
}
