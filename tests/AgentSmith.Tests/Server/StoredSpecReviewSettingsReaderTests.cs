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
    public async Task StoredSpecReviewSettingsReader_StoreThatNeverLoaded_Throws()
    {
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.GetSetting(It.IsAny<string>())).Throws(new InvalidOperationException("db down"));
        var reader = new StoredSpecReviewSettingsReader(store.Object);

        reader.Invoking(r => r.Current()).Should().Throw<InvalidOperationException>();
        var result = await new ReviewLoopRig().Handler(reader).ExecuteAsync(
            new ReviewSpecsContext(null, [], new AgentConfig(), new ReviewLoopRig().Pipeline), CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("The spec_review settings could not be read: db down");
    }

    [Fact]
    public void StoredSpecReviewSettingsReader_StoreWithoutASpecReviewDocument_ReadsTheDefaults()
    {
        using var h = new DbConfigTestHarness();
        h.Import("orchestrator:\n  max_run_wall_time_seconds: 5400\n");

        var settings = new StoredSpecReviewSettingsReader(h.Store).Current();

        (settings.Review.Rounds, settings.Review.Reviewers, settings.Review.MaxConcurrent, settings.Review.BudgetSharePercent)
            .Should().Be((4, 2, 2, 40));
        settings.MaxRunWallTimeSeconds.Should().Be(5400);
    }

    [Fact]
    public async Task ReviewSpecs_StoreEditOfRounds_AppliesToTheNextRunWithoutRebuildingTheProvider()
    {
        using var h = new DbConfigTestHarness();
        h.Import("spec_review:\n  rounds: 3\n");
        var services = new ServiceCollection();
        services.AddSingleton(h.Store);
        services.AddSingleton<ISpecReviewSettingsReader, StoredSpecReviewSettingsReader>();
        using var provider = services.BuildServiceProvider();
        var reader = provider.GetRequiredService<ISpecReviewSettingsReader>();
        reader.Current().Review.Rounds.Should().Be(3);

        h.Store.SaveSetting("spec_review", JsonDocument.Parse("""{"rounds":0}""").RootElement,
            new ChangeAttribution("operator"));
        var rig = new ReviewLoopRig();
        var result = await rig.Handler(provider.GetRequiredService<ISpecReviewSettingsReader>()).ExecuteAsync(
            new ReviewSpecsContext(null, [], new AgentConfig(), rig.Pipeline), CancellationToken.None);

        result.Message.Should().Be("spec review skipped: spec_review.rounds is 0");
        rig.Inputs.Should().BeEmpty();
    }

    [Fact]
    public void ReviewSpecsHandler_ResolvesFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ICommandHandler<ReviewSpecsContext>>()
            .Should().BeOfType<AgentSmith.Application.Services.Handlers.ReviewSpecsHandler>();
        services.Where(d => d.ServiceType == typeof(ISpecReviewSettingsReader)).Should().ContainSingle()
            .Which.ImplementationType.Should().Be(typeof(StoredSpecReviewSettingsReader));
        scope.ServiceProvider.GetRequiredService<SpecReviewLoop>().Should().NotBeNull();
    }
}
