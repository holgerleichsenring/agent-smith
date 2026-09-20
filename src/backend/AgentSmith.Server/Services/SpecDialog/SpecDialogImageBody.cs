using AgentSmith.Contracts.Models;
using Microsoft.AspNetCore.Http.Features;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-3af8: the bytes of one image upload, BOUNDED BEFORE THE BODY IS READ.
/// <para>
/// This is the inverse of the archive route's ceiling, which raises the request limit for the
/// one route that needs a larger one. An image upload needs a SMALLER one, and needs it applied
/// where the archive's is applied — on the request, before anything reads it. What bounds an
/// upload is not what bounds a download: the ticket path checks its cap after a tracker's
/// response has already been buffered, which a route facing the public must not copy.
/// </para>
/// <para>
/// Two mechanisms, because a client controls which one it meets. A DECLARED length over the
/// bound is refused without touching the stream. A body that declares nothing is read against
/// the bound and refused the moment it passes it — and the lowered ceiling is what makes the
/// server itself refuse it first wherever the server enforces one.
/// </para>
/// </summary>
public sealed class SpecDialogImageBody(ILogger<SpecDialogImageBody> logger)
{
    /// <summary>The largest image this route accepts — the ticket path's five megabytes.</summary>
    public const long Bytes = TicketImageAttachment.MaxSizeBytes;

    /// <summary>
    /// The uploaded bytes, or null when the upload is over the bound. Null is a refusal the
    /// caller answers with; it is never an empty image.
    /// </summary>
    public async Task<byte[]?> ReadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        Lower(context);
        if (context.Request.ContentLength is { } declared && declared > Bytes)
        {
            logger.LogInformation(
                "A design-conversation image upload declared {Declared} byte(s), over the "
                + "{Bound}-byte bound — refused without reading the body.", declared, Bytes);
            return null;
        }

        var read = new MemoryStream();
        var copied = await CopyBoundedAsync(context.Request.Body, read, cancellationToken);
        if (copied) return read.ToArray();
        logger.LogInformation(
            "A design-conversation image upload passed the {Bound}-byte bound while being "
            + "read — refused.", Bytes);
        return null;
    }

    // One byte over the bound is enough to refuse, so the read never holds more than that.
    private static async Task<bool> CopyBoundedAsync(
        Stream body, Stream into, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (into.Length + read > Bytes) return false;
            await into.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return true;
    }

    // Where the server enforces a ceiling at all, it is the server that refuses first.
    private void Lower(HttpContext context)
    {
        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is null || limit.IsReadOnly)
        {
            logger.LogDebug(
                "No request-body ceiling could be lowered for an image upload; the server "
                + "exposes none, or the body is already being read.");
            return;
        }

        limit.MaxRequestBodySize = Bytes;
    }
}
