using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Design;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Design;

/// <summary>
/// 2026-10-01-7f7ac: a typed client of its own, with no token and no redirects, that downloads a
/// rendered node. Only https on Figma's image host is fetched, at most the tool-image size cap,
/// and only a body that starts with the PNG signature is returned. Neither the url nor an
/// exception message is repeated in a failure or a log line — only statuses and types.
/// </summary>
public sealed class FigmaImageDownloader(HttpClient http, ILogger<FigmaImageDownloader> logger) : IFigmaImageDownloader
{
    /// <summary>The storage host Figma's images endpoint is observed to answer with; the
    /// documentation names none, so anything else is refused rather than guessed.</summary>
    internal static readonly IReadOnlySet<string> ImageHosts =
        new HashSet<string>(["figma-alpha-api.s3.us-west-2.amazonaws.com"], StringComparer.OrdinalIgnoreCase);

    internal const long MaxBytes = TicketImageAttachment.MaxSizeBytes;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public async Task<FigmaExportResult> DownloadPngAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort || uri.UserInfo.Length > 0 || !ImageHosts.Contains(uri.Host))
            return FigmaExportResult.Failed("the export url is not https on Figma's image host, so it was not fetched");
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return FigmaExportResult.Failed($"the image download answered HTTP {(int)response.StatusCode}");
            if (response.Content.Headers.ContentLength > MaxBytes)
                return OverCap();
            var bytes = await ReadCappedAsync(response.Content, cancellationToken);
            if (bytes is null) return OverCap();
            return bytes.AsSpan().StartsWith(PngSignature)
                ? FigmaExportResult.Of(bytes)
                : FigmaExportResult.Failed("the downloaded image is not a PNG");
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning("Figma image download failed: {ExceptionType}", ex.GetType().Name);
            return FigmaExportResult.Failed(new FigmaReadFailure(
                FigmaReadFailureKind.Unreachable, $"Figma's image host did not answer ({ex.GetType().Name})"));
        }
    }

    private static FigmaExportResult OverCap() =>
        FigmaExportResult.Failed($"the rendered image is over the {MaxBytes}-byte limit");

    /// <summary>The body, or null when it runs past the cap — read no further than one byte over.</summary>
    private static async Task<byte[]?> ReadCappedAsync(HttpContent content, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes) return null;
        }
        return buffer.ToArray();
    }
}
