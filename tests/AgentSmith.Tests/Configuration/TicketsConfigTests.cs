using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>2026-11-17-f9b0d: the tickets: block binds from agentsmith.yml with the values the operator ratified as
/// defaults, and a value below 1 is refused naming its key.</summary>
public sealed class TicketsConfigTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"agentsmith-tickets-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private AgentSmithConfig Load(string yaml)
    {
        File.WriteAllText(_tempFile, yaml);
        return new YamlConfigurationLoader(new RawConfigMaterializer(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(),
            new DeploymentDefaultsApplier(), new ConfigCatalogResolver(), new AgentSmithPaths()), new NoOpSystemEventPublisher())
            .LoadConfig(_tempFile);
    }

    [Fact]
    public void Tickets_Default_IsTheRatifiedValuesAndValid()
    {
        var config = Load("projects: {}\n");
        var t = config.Tickets;

        (t.RequiresLinkDeadlineSeconds, t.DependentsWakeWindowHours, t.DependentsWakeCap, t.PrRereadIntervalSeconds,
            t.PrRereadStaleSeconds, t.PrRereadOpenCap, t.PrRereadMergedCap).Should().Be((60, 24, 50, 300, 900, 50, 50));
        TicketsConfigValidator.Findings(config).Should().BeEmpty();
    }

    [Fact]
    public void Tickets_BindsFromYaml()
    {
        var t = Load("projects: {}\ntickets:\n  requires_link_deadline_seconds: 30\n  pr_reread_merged_cap: 7\n").Tickets;

        (t.RequiresLinkDeadlineSeconds, t.PrRereadMergedCap).Should().Be((30, 7));
    }

    [Fact]
    public void Tickets_ZeroValue_RefusedNamingItsKey()
    {
        var config = Load("projects: {}\ntickets:\n  pr_reread_interval_seconds: 0\n");

        TicketsConfigValidator.Findings(config).Should().ContainSingle(f => f.IsBlocking && f.Field == "tickets")
            .Which.Reason.Should().Be("tickets.pr_reread_interval_seconds is 0; it must be 1 or more");
    }
}
