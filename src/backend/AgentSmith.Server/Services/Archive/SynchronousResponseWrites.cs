using Microsoft.AspNetCore.Http.Features;

namespace AgentSmith.Server.Services.Archive;

/// <summary>
/// 2026-08-28-3793: lets the ONE route that streams a zip write to its response
/// synchronously.
/// <para>
/// Kestrel disallows synchronous writes by default, and <c>ZipArchive</c> makes them: it
/// closes each entry by writing that entry's data descriptor through
/// <c>Stream.Write</c>, with no asynchronous path to take instead. Without this the export
/// dies the moment the first table's entry is closed — after the headers are out, so the
/// caller sees a truncated body under a 200 rather than an error. Switching it on here
/// rather than in the host leaves every other route asynchronous.
/// </para>
/// </summary>
public sealed class SynchronousResponseWrites(ILogger<SynchronousResponseWrites> logger)
{
    /// <summary>True when synchronous writes are now allowed for this request.</summary>
    public bool Allow(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var control = context.Features.Get<IHttpBodyControlFeature>();
        if (control is null)
        {
            logger.LogDebug(
                "This server exposes no body-control feature, so an archive download writes "
                + "under whatever mode it already has.");
            return false;
        }

        control.AllowSynchronousIO = true;
        return true;
    }
}
