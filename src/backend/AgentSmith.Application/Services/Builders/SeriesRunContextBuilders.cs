using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-0207a, 0207d: the context builders of a series run's steps — the filing's last, the beat's
/// three and the series check's own.</summary>
internal static class SeriesRunContextBuilders
{
    internal static void Add(IServiceCollection services)
    {
        Add<OpenSeriesRunContextBuilder>(services, CommandNames.OpenSeriesRun);
        Add<ReadSeriesContextBuilder>(services, CommandNames.ReadSeries);
        Add<SeriesMasterContextBuilder>(services, CommandNames.SeriesMaster);
        Add<PostSeriesStatusContextBuilder>(services, CommandNames.PostSeriesStatus);
        Add<VerifySeriesContextBuilder>(services, CommandNames.VerifySeries);
    }

    private static void Add<TBuilder>(IServiceCollection services, string commandName)
        where TBuilder : IContextBuilder, new()
        => services.AddSingleton(new KeyedContextBuilder(commandName, new TBuilder()));
}
