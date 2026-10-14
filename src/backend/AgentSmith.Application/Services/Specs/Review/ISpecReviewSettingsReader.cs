namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: the spec review's settings, read LIVE on each call — the server's configuration object
/// is built once, so an edit would otherwise wait for a restart. A reader that cannot read throws; the
/// step then fails naming why, never reviewing on defaults nobody configured.
/// </summary>
public interface ISpecReviewSettingsReader
{
    SpecReviewSettings Current();
}
