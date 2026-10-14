using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-13-8762u: the series settings a test reads — the defaults (24 / 8 / unmeasured) over a 1800 s
/// wall clock unless it says otherwise.</summary>
internal static class SeriesSettingsDoubles
{
    public const int WallSeconds = 1800;

    public static SeriesConfig Defaults => new();

    public static SeriesSettings Settings(SeriesConfig? series = null, int wallSeconds = WallSeconds) =>
        new(series ?? new SeriesConfig(), wallSeconds);

    public static ISeriesSettingsReader Reader(SeriesConfig? series = null, int wallSeconds = WallSeconds) =>
        new Fixed(Settings(series, wallSeconds));

    public static SeriesChunkStop ChunkStop(ITicketProviderFactory? tickets = null) =>
        new(tickets ?? Moq.Mock.Of<ITicketProviderFactory>(), NullLogger<SeriesChunkStop>.Instance);

    /// <summary>The real banner over its three voices; a banner reads no sandbox, so the shortfall's reverter is none.</summary>
    public static CodePullRequestBanner Banners(ITicketProviderFactory? tickets = null) =>
        new(new ShortfallDelivery(null!, NullLogger<ShortfallDelivery>.Instance),
            new FailedRunPersistence(new WipCommitMessage()), ChunkStop(tickets));

    private sealed class Fixed(SeriesSettings settings) : ISeriesSettingsReader
    {
        public SeriesSettings Current() => settings;
    }
}
