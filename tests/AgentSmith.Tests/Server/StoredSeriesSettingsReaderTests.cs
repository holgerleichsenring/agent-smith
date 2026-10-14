using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.ConfigStudio;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-13-8762u: the server reads the series block from the config store per call — a Studio save applies to
/// the next run without a restart, a store without the document reads the defaults, and a document of the wrong
/// type throws naming which of the two documents it was.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class StoredSeriesSettingsReaderTests
{
    [Fact]
    public void StoredSeriesSettingsReader_StudioSave_IsReadByTheNextCall()
    {
        using var h = new DbConfigTestHarness();
        h.Import("orchestrator:\n  max_run_wall_time_seconds: 5400\n");
        var reader = new StoredSeriesSettingsReader(h.Store);
        (reader.Current().Series.MaxSpecs, reader.Current().Series.SpecsPerAttempt, reader.Current().RunWallSeconds)
            .Should().Be((24, 8, 5400));

        h.Store.SaveSetting("series", JsonDocument.Parse("""{"specsPerAttempt":4,"secondsPerSpec":900}""").RootElement,
            new ChangeAttribution("operator"));

        (reader.Current().Series.SpecsPerAttempt, reader.Current().Series.SecondsPerSpec).Should().Be((4, 900));
    }

    [Fact]
    public void StoredSeriesSettingsReader_WrongTypeDocument_NamesWhichDocument()
    {
        var series = new Mock<IConfigStore>();
        series.Setup(s => s.GetSetting(It.IsAny<string>())).Returns(new OrchestratorGlobalConfig());
        var orchestrator = new Mock<IConfigStore>();
        orchestrator.Setup(s => s.GetSetting(It.IsAny<string>())).Returns(new SeriesConfig());

        new StoredSeriesSettingsReader(series.Object).Invoking(r => r.Current()).Should()
            .Throw<InvalidOperationException>().WithMessage("The stored series settings are not a series block.");
        new StoredSeriesSettingsReader(orchestrator.Object).Invoking(r => r.Current()).Should()
            .Throw<InvalidOperationException>().WithMessage("The stored orchestrator settings are not an orchestrator block.");
    }

    [Fact]
    public void ConfigStore_SeriesSection_RoundTripsAsSingletonDoc()
    {
        using var h = new DbConfigTestHarness();
        h.Import("series:\n  max_specs: 20\n  specs_per_attempt: 5\n  seconds_per_spec: 1294\n");

        var raw = h.Assembler.Assemble(h.DocStore.LoadAll());

        (raw.Series.MaxSpecs, raw.Series.SpecsPerAttempt, raw.Series.SecondsPerSpec).Should().Be((20, 5, 1294));
        h.Store.SettingTypes.Should().Contain("series");
        h.Store.GetSetting("series").Should().BeOfType<SeriesConfig>().Which.SpecsPerAttempt.Should().Be(5);
    }

    [Fact]
    public void SeriesSettingsReader_ResolvesFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        services.Where(d => d.ServiceType == typeof(ISeriesSettingsReader)).Should().ContainSingle()
            .Which.ImplementationType.Should().Be(typeof(StoredSeriesSettingsReader));
        scope.ServiceProvider.GetRequiredService<ICommandHandler<SpecSequenceContext>>()
            .Should().BeOfType<AgentSmith.Application.Services.Handlers.SpecSequenceHandler>();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Application.Services.Handlers.SeriesChunkStop>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgentSmith.Server.Services.Tickets.TicketApprovalPreview>().Should().NotBeNull();
    }
}
