namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762u: the series settings, read LIVE on each call — the server's configuration object is built
/// once, so an edit would otherwise wait for a restart. A reader that cannot read throws.
/// </summary>
public interface ISeriesSettingsReader
{
    SeriesSettings Current();
}
