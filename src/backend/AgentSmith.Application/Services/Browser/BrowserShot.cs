namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: one screenshot render.mjs wrote — its viewport, the base64 file it lies in,
/// its pixel size after downscaling, and how much of the page's height it covers.
/// </summary>
public sealed record BrowserShot(
    string Viewport, string File, int Width, int Height, int PageHeight, int CapturedHeight);
