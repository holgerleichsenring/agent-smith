using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992e: the final pull requests' merge and CI acts over a host double, for the master's tests.</summary>
internal static class SeriesFinalDoubles
{
    public static SeriesFinalMerge Merge(ISourceProviderFactory? sources = null, ISeriesRunLaunch? launch = null)
    {
        var hosts = sources ?? Mock.Of<ISourceProviderFactory>();
        var completer = new PullRequestCompleter(hosts, Mock.Of<IEventPublisher>(), NullLogger<PullRequestCompleter>.Instance);
        return new SeriesFinalMerge(completer, launch ?? Mock.Of<ISeriesRunLaunch>());
    }

    public static SeriesFinalCi Ci(ISourceProviderFactory? sources = null)
    {
        var hosts = sources ?? Mock.Of<ISourceProviderFactory>();
        return new SeriesFinalCi(hosts, new PullRequestBuildRequeue(hosts, NullLogger<PullRequestBuildRequeue>.Instance));
    }
}
