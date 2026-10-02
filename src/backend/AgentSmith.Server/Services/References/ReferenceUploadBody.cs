using AgentSmith.Server.Models;
using Microsoft.AspNetCore.Http.Features;

namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-01-283db: the parts of one website upload, BOUNDED BEFORE THE BODY IS READ — the image
/// upload's two mechanisms at the route's own ceiling. A declared length over it is refused
/// without touching the stream; the ceiling is set on the request, where the server enforces one,
/// and the multipart reader is held to it as well, so an undeclared body stops at it too.
/// Each part's file name is its path inside the set.
/// </summary>
public sealed class ReferenceUploadBody(ILogger<ReferenceUploadBody> logger)
{
    // Room for the ignored and skipped files a copied folder carries beside the ones it keeps
    // (2026-10-02-0d72: a .git folder alone holds hundreds); the body ceiling still bounds them all.
    private const int MaxParts = ReferenceUploadLimits.MaxFiles * 10;

    /// <summary>The uploaded parts, or null when the body is over the route's ceiling.</summary>
    public async Task<IReadOnlyList<ReferenceUploadPart>?> ReadAsync(HttpContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        Bound(context);
        if (context.Request.ContentLength is > ReferenceUploadLimits.RouteBodyBytes)
        {
            logger.LogInformation("A website upload declared {Declared} byte(s), over the {Bound}-byte "
                + "ceiling — refused without reading the body.", context.Request.ContentLength, ReferenceUploadLimits.RouteBodyBytes);
            return null;
        }
        if (!context.Request.HasFormContentType) return [];
        try
        {
            var form = await context.Request.ReadFormAsync(Options, ct);
            return await PartsAsync(form.Files, ct);
        }
        catch (InvalidDataException ex)
        {
            logger.LogInformation(ex, "A website upload passed a multipart bound while being read — refused.");
            return null;
        }
    }

    private static readonly FormOptions Options = new()
    {
        MultipartBodyLengthLimit = ReferenceUploadLimits.RouteBodyBytes,
        ValueCountLimit = MaxParts,
    };

    private static async Task<IReadOnlyList<ReferenceUploadPart>> PartsAsync(IFormFileCollection files, CancellationToken ct)
    {
        var parts = new List<ReferenceUploadPart>(files.Count);
        foreach (var file in files)
        {
            using var into = new MemoryStream();
            await file.CopyToAsync(into, ct);
            parts.Add(new ReferenceUploadPart(file.FileName, into.ToArray()));
        }
        return parts;
    }

    // Where the server enforces a ceiling at all, it is the server that refuses first.
    private void Bound(HttpContext context)
    {
        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is null || limit.IsReadOnly)
        {
            logger.LogDebug("No request-body ceiling could be set for a website upload; the server "
                + "exposes none, or the body is already being read.");
            return;
        }
        limit.MaxRequestBodySize = ReferenceUploadLimits.RouteBodyBytes;
    }
}
