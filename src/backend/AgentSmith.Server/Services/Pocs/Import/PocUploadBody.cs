using Microsoft.AspNetCore.Http.Features;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: the one ZIP of a PoC upload, BOUNDED BEFORE THE BODY IS READ at pocs.archive.max_zip_mb plus room
/// for the multipart framing — ReferenceUploadBody's two mechanisms at this route's ceiling. A declared length over it is
/// refused without touching the stream; the multipart reader is held to it too, so an undeclared body stops at it.
/// </summary>
internal sealed class PocUploadBody(ILogger<PocUploadBody> logger)
{
    private const long FramingBytes = 1024 * 1024; // not a limit: room for the multipart framing around the one file
    private const int MaxParts = 8; // not a limit: one file and the target field, with room for what a browser adds

    /// <summary>The ceiling for <paramref name="maxZipMb"/>.</summary>
    public static long CeilingOf(int maxZipMb) => maxZipMb * 1024L * 1024L + FramingBytes;

    /// <summary>The upload, an empty upload (no file), or null when the body is over the ceiling.</summary>
    public async Task<PocUpload?> ReadAsync(HttpContext context, int maxZipMb, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ceiling = CeilingOf(maxZipMb);
        Bound(context, ceiling);
        if (context.Request.ContentLength > ceiling)
        {
            logger.LogInformation("A PoC upload declared {Declared} byte(s), over the {Bound}-byte ceiling — refused unread.",
                context.Request.ContentLength, ceiling);
            return null;
        }
        if (!context.Request.HasFormContentType) return new PocUpload(string.Empty, [], null);
        try
        {
            var form = await context.Request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = ceiling, ValueCountLimit = MaxParts }, ct);
            return await PartAsync(form, ct);
        }
        catch (InvalidDataException ex)
        {
            logger.LogInformation(ex, "A PoC upload passed a multipart bound while being read — refused.");
            return null;
        }
    }

    private static async Task<PocUpload> PartAsync(IFormCollection form, CancellationToken ct)
    {
        var target = form.TryGetValue("target", out var value) ? value.ToString() : null;
        if (form.Files.Count != 1) return new PocUpload(string.Empty, [], target);
        using var into = new MemoryStream();
        await form.Files[0].CopyToAsync(into, ct);
        return new PocUpload(form.Files[0].FileName, into.ToArray(), target);
    }

    private void Bound(HttpContext context, long ceiling)
    {
        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is null || limit.IsReadOnly)
        {
            logger.LogDebug("No request-body ceiling could be set for a PoC upload; the server exposes none.");
            return;
        }
        limit.MaxRequestBodySize = ceiling;
    }
}
