using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-11-25-9992g: the series wake's parts for tests that build their subject by hand — by default no series
/// holds any ticket, so the standalone paths run as before.</summary>
internal static class SeriesWakeDoubles
{
    public static SeriesPrCommentWake PrComments(IPrTicketOwner? owners = null, ISeriesRunStore? store = null, ISeriesWakes? wakes = null) =>
        new(owners ?? Mock.Of<IPrTicketOwner>(), store ?? Mock.Of<ISeriesRunStore>(), wakes ?? Mock.Of<ISeriesWakes>(),
            NullLogger<SeriesPrCommentWake>.Instance);

    public static SeriesTicketCommentWake TicketComments(ISeriesRunStore? store = null, ISeriesWakes? wakes = null,
        AgentSmith.Contracts.Reviews.IPrReviewAuthorTrust? trust = null) =>
        new(store ?? Mock.Of<ISeriesRunStore>(), wakes ?? Mock.Of<ISeriesWakes>(),
            trust ?? Mock.Of<AgentSmith.Contracts.Reviews.IPrReviewAuthorTrust>(), NullLogger<SeriesTicketCommentWake>.Instance);

    public static SeriesBudgetQuestion Budget(ISeriesRunStore store, AgentSmithConfig? config = null) => new(store, config ?? new AgentSmithConfig());
}
