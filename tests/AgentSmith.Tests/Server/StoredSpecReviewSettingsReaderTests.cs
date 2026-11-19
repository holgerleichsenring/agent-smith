using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.ConfigStudio;
using AgentSmith.Tests.Specs.Review;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-13-8762g: the server reads the spec review's settings from the config store per run — a Studio edit
/// applies to the next run without a restart, a store without the document reads the defaults, and a store
/// that never loaded fails the step instead of reviewing on defaults nobody read.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class StoredSpecReviewSettingsReaderTests
{
    [Fact]
    public void StoredSpecReviewSettingsReader_StoreThatNeverLoaded_Throws()
    {
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.GetSetting(It.IsAny<string>())).Throws(new InvalidOperationException("db down"));
        var reader = new StoredSpecReviewSettingsReader(store.Object);

        reader.Invoking(r => r.Current()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StoredSpecReviewSettingsReader_StoreWithoutASpecReviewDocument_ReadsTheDefaults()
    {
        using var h = new DbConfigTestHarness();
        h.Import("orchestrator:\n  max_run_wall_time_seconds: 5400\n");

        var settings = new StoredSpecReviewSettingsReader(h.Store).Current();

        settings.Review.RegroundReadBudget.Should().Be(24);
    }

    [Fact]
    public void SpecReviewSettings_StoreEditOfTheReGroundBudget_AppliesToTheNextReadWithoutRebuildingTheProvider()
    {
        using var h = new DbConfigTestHarness();
        h.Import("spec_review:\n  reground_read_budget: 3\n");
        var services = new ServiceCollection();
        services.AddSingleton(h.Store);
        services.AddSingleton<ISpecReviewSettingsReader, StoredSpecReviewSettingsReader>();
        using var provider = services.BuildServiceProvider();
        var reader = provider.GetRequiredService<ISpecReviewSettingsReader>();
        reader.Current().Review.RegroundReadBudget.Should().Be(3);

        h.Store.SaveSetting("spec_review", JsonDocument.Parse("""{"regroundReadBudget":9}""").RootElement,
            new ChangeAttribution("operator"));
        provider.GetRequiredService<ISpecReviewSettingsReader>().Current().Review.RegroundReadBudget.Should().Be(9);
    }

    [Fact]
    public void SpecReviewSettingsReader_ResolvesFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        services.Where(d => d.ServiceType == typeof(ISpecReviewSettingsReader)).Should().ContainSingle()
            .Which.ImplementationType.Should().Be(typeof(StoredSpecReviewSettingsReader));
    }
}
