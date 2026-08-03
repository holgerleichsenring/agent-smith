using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0312d: <c>agent.parallelism.max_concurrent_skill_rounds</c> fanned out consecutive
/// same-(Name, Round) skill rounds. p0312a removed the last batchable command family and
/// this phase removed the batch path itself, so the knob has no reader left. It is gone
/// from the schema rather than kept as a setting that silently does nothing.
///
/// Two things must hold together, and they pull in opposite directions: the key must be
/// ABSENT from the model (nothing can read it, nothing can pretend to), and a deployed
/// agentsmith.yml that still carries it must keep loading — an operator's server must not
/// fail to start over a key that stopped mattering.
/// </summary>
public sealed class RetiredParallelismConfigTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(),
        $"agentsmith-parallelism-yaml-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private AgentSmithConfig Load(string yaml)
    {
        File.WriteAllText(_tempFile, yaml);
        return new YamlConfigurationLoader(
                new RawConfigMaterializer(
                    new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(),
                    new DeploymentDefaultsApplier(), new ConfigCatalogResolver(), new AgentSmithPaths()),
                new NoOpSystemEventPublisher())
            .LoadConfig(_tempFile);
    }

    [Fact]
    public void Config_MaxConcurrentSkillRounds_IsEitherReadOrAbsent()
    {
        // Absent is the branch this phase took. Asserted over the whole configuration
        // contract, not just AgentConfig, so re-introducing the knob anywhere in the
        // schema — under a different parent, under a different name for the same thing —
        // has to come here and state its reader.
        var configTypes = typeof(AgentConfig).Assembly
            .GetTypes()
            .Where(t => t.Namespace == typeof(AgentConfig).Namespace);
        var offenders = configTypes
            .SelectMany(t => t.GetProperties().Select(p => $"{t.Name}.{p.Name}"))
            .Where(n => n.Contains("SkillRounds", StringComparison.OrdinalIgnoreCase)
                        || n.EndsWith(".Parallelism", StringComparison.OrdinalIgnoreCase))
            .ToList();

        offenders.Should().BeEmpty(
            "p0312d removed the batch path; a parallelism knob in the config schema would "
            + "have no reader and would silently do nothing");
    }

    [Fact]
    public void Load_ConfigStillDeclaringTheRetiredKey_LoadsWithoutError()
    {
        // The loader ignores unmatched properties, which is what makes removing an
        // operator-visible key safe. Pinned explicitly: this is the guarantee, not an
        // incidental deserializer setting someone may tighten later without noticing.
        var cfg = Load("""
            agents:
              claude:
                type: anthropic
                model: claude-sonnet-4-20250514
                parallelism:
                  max_concurrent_skill_rounds: 4
            projects: {}
            secrets: {}
            """);

        cfg.Agents.Should().ContainKey("claude");
        cfg.Agents["claude"].Model.Should().Be("claude-sonnet-4-20250514");
    }
}
