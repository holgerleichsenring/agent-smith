using System.Security.Claims;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs;
using AgentSmith.Server.Services.Pocs.Import;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-933da: the PoCs area. Every PoC route is mapped on one group behind <see cref="PocsGate"/>, so with
/// pocs.enabled off each answers an empty 404 to a caller holding its permissions. The group's prefix is /api and each
/// route names /pocs itself, so the list's pattern is /api/pocs without the trailing slash an empty route would add. The list needs pocs.read, which is
/// global-only, so it declares no project; its rows are filtered by the caller's runs.read projects. PoC-keyed routes
/// of later slices are /api/pocs/{pocId:long}/… with InProject(ProjectFrom.Poc).
/// 2026-11-23-933db: an upload (one ZIP, bounded at pocs.archive.max_zip_mb) and its Start need pocs.write, global and
/// with no project source: the upload belongs to the caller, read back with their own key.
/// </summary>
internal static class PocEndpoints
{
    internal static WebApplication MapPocEndpoints(this WebApplication app)
    {
        var pocs = app.MapGroup("/api").AddEndpointFilter<PocsGate>();
        pocs.MapGet("/pocs", ListAsync).Needs(Permissions.PocsRead);
        pocs.MapPost("/pocs/uploads", UploadAsync).Needs(Permissions.PocsWrite);
        pocs.MapPost("/pocs/uploads/{uploadId}/import", ImportAsync).Needs(Permissions.PocsWrite);
        pocs.MapPocRevisionEndpoints(); // 2026-11-23-933dc
        pocs.MapPocTargetEndpoints(); // 2026-11-23-933dd
        return app;
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> ListAsync(ClaimsPrincipal user, PocListReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ReadAsync(user, cancellationToken));

    internal static async Task<IResult> UploadAsync(
        HttpContext context, PocUploadBody body, PocUploadAdmission admission, TicketProjects projects, CancellationToken cancellationToken)
    {
        var maxZipMb = projects.Config().Pocs.Archive.MaxZipMb;
        if (await body.ReadAsync(context, maxZipMb, cancellationToken) is not { } upload)
            return Results.Text($"The upload is larger than pocs.archive.max_zip_mb ({maxZipMb} MB), the bound that keeps an upload "
                + "from filling the server's memory.", "text/plain", statusCode: StatusCodes.Status413PayloadTooLarge);
        var (answer, refusal) = await admission.AdmitAsync(context.User, upload, cancellationToken);
        return answer is not null ? Results.Ok(answer) : refusal!.Answer();
    }

    internal static async Task<IResult> ImportAsync(
        string uploadId, PocImportRequest? request, ClaimsPrincipal user, PocImportLauncher launcher, CancellationToken cancellationToken)
    {
        if (request is null) return PocImportRefusals.BadChoice.Answer();
        var (answer, refusal) = await launcher.ImportAsync(user, uploadId, request, cancellationToken);
        return answer is not null ? Results.Json(answer, statusCode: StatusCodes.Status201Created) : refusal!.Answer();
    }
}
