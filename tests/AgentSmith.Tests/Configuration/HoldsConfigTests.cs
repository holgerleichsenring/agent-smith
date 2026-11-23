using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>2026-11-23-31dci: holds.escalate_after_minutes binds from agentsmith.yml with a default of 60, 0 turns the
/// report off, and a negative value is refused at load with a message naming the value, the reason and the key.</summary>
public sealed class HoldsConfigTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"agentsmith-holds-{Guid.NewGuid():N}.yml");

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
    public void EscalateAfter_Default_Is60AndValid_BindsFromYaml()
    {
        var config = Load("projects: {}\n");

        config.Holds.EscalateAfterMinutes.Should().Be(60);
        HoldsConfigValidator.Findings(config).Should().BeEmpty();
        Load("projects: {}\nholds:\n  escalate_after_minutes: 15\n").Holds.EscalateAfterMinutes.Should().Be(15);
    }

    [Fact]
    public void EscalateAfter_Zero_IsOffAndValid() =>
        new HoldsConfig { EscalateAfterMinutes = 0 }.Problems().Should().BeEmpty();

    [Fact]
    public void EscalateAfter_Negative_RefusedAtLoadNamingValueReasonAndKey()
    {
        var config = Load("projects: {}\nholds:\n  escalate_after_minutes: -5\n");

        var refusal = new AgentSmithConfigValidator(new SkillsConfigValidator()).Validate(config).Should().ContainSingle().Subject;
        refusal.Should().Contain("holds.escalate_after_minutes").And.Contain("is -5").And.Contain("outside the run");
        HoldsConfigValidator.Findings(config).Should().ContainSingle(f => f.IsBlocking && f.Field == "holds");
    }
}
