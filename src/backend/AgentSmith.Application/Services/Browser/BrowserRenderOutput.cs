namespace AgentSmith.Application.Services.Browser;

/// <summary>2026-10-01-283de: one finished render — the script's report and each screenshot's JPEG bytes, in report order.</summary>
public sealed record BrowserRenderOutput(BrowserRenderResult Result, IReadOnlyList<byte[]> Shots);
