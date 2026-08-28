using Microsoft.AspNetCore.Http.Features;

namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: raises the request-body ceiling for the ONE route that needs it.
/// <para>
/// Kestrel's default is thirty megabytes, which is smaller than any archive worth
/// restoring, and nothing in this server raises it because nothing has needed to. Raising
/// it here — on the request, through the feature the server exposes for exactly this —
/// leaves every other route at the default, so one large upload does not widen the surface
/// of all of them.
/// </para>
/// </summary>
public sealed class ArchiveUploadCeiling(ILogger<ArchiveUploadCeiling> logger)
{
    /// <summary>The largest archive this endpoint accepts: eight gibibytes.</summary>
    public const long Bytes = 8L * 1024 * 1024 * 1024;

    /// <summary>
    /// The ceiling now in force for <paramref name="context"/>, or null where the server
    /// enforces none at all (a test host, or a body already being read).
    /// </summary>
    public long? Raise(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is null || limit.IsReadOnly)
        {
            logger.LogDebug(
                "No request-body ceiling could be raised for an archive upload; the server "
                + "exposes none, or the body is already being read.");
            return null;
        }

        limit.MaxRequestBodySize = Bytes;
        return Bytes;
    }
}
