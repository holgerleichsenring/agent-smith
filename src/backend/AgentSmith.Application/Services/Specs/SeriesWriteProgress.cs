namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7f: how far a multi-repository spec write got — each member's HEAD before the write,
/// the sha each member's sub-series stands at, and whether this write pushed it.
/// </summary>
public sealed record SeriesWriteProgress(
    IReadOnlyDictionary<string, string> Before,
    Dictionary<string, string> Shas,
    Dictionary<string, bool> Pushed);
