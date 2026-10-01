namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: one finished render — the script's report and each screenshot's JPEG bytes, in
/// report order. 2026-10-01-283dh: and what staging the source had to say, such as a file not copied.
/// </summary>
public sealed record BrowserRenderOutput(
    BrowserRenderResult Result, IReadOnlyList<byte[]> Shots, IReadOnlyList<string>? Notes = null);
