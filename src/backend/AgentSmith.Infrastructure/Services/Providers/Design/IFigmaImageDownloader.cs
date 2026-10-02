using AgentSmith.Contracts.Models.Design;

namespace AgentSmith.Infrastructure.Services.Providers.Design;

/// <summary>
/// 2026-10-01-7f7ac: fetches the PNG an images call pointed at. It holds no secret by
/// construction: the url is a storage link, and the Figma token must never travel there.
/// </summary>
public interface IFigmaImageDownloader
{
    /// <summary>The PNG behind <paramref name="url"/>, or why it was not taken.</summary>
    Task<FigmaExportResult> DownloadPngAsync(string url, CancellationToken cancellationToken);
}
