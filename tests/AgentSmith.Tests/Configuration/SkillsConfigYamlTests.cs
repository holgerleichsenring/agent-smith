using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using AgentSmith.Application.Services.Events;

namespace AgentSmith.Tests.Configuration;

public sealed class SkillsConfigYamlTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(),
        $"agentsmith-skills-yaml-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private AgentSmithConfig Load(string yaml)
    {
        File.WriteAllText(_tempFile, yaml);
        return new YamlConfigurationLoader(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(), new ConfigCatalogResolver(), new AgentSmithPaths(), new NoOpSystemEventPublisher())
            .LoadConfig(_tempFile);
    }

    [Fact]
    public void SkillsBlock_VersionPopulated_FromCamelCaseAndSnakeCase()
    {
        var cfg = Load("""
            projects: {}
            skills:
              source: default
              version: v1.0.0
              cacheDir: /var/lib/agentsmith/skills
            secrets: {}
            """);

        cfg.Skills.Source.Should().Be(SkillsSourceMode.Default);
        cfg.Skills.Version.Should().Be("v1.0.0");
    }

    [Fact]
    public void DefaultCacheDir_PrefersXdgCacheHome()
    {
        var prev = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", "/some/xdg");
            DefaultPaths.ComputeSkillsCatalogRoot().Should().Be("/some/xdg/agentsmith/skills");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", prev);
        }
    }

    [Fact]
    public void DefaultCacheDir_FallsBackToHomeCache()
    {
        var prevXdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        var prevHome = Environment.GetEnvironmentVariable("HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", null);
            Environment.SetEnvironmentVariable("HOME", "/home/user");
            DefaultPaths.ComputeSkillsCatalogRoot().Should().Be("/home/user/.cache/agentsmith/skills");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", prevXdg);
            Environment.SetEnvironmentVariable("HOME", prevHome);
        }
    }

    [Fact]
    public void SkillsBlock_DefaultCacheDir_IsUserScoped()
    {
        var cfg = Load("""
            projects: {}
            skills:
              source: default
              version: v1.0.0
            secrets: {}
            """);

        cfg.Skills.CacheDir.Should().NotBe("/var/lib/agentsmith/skills");
        cfg.Skills.CacheDir.Should().Contain("agentsmith").And.Contain("skills");
    }

    // p0325: skills ship embedded in the release — no skills block means the
    // embedded catalog, and explicit config (path > url > version) keeps
    // today's fetch behavior byte-identical.
    [Fact]
    public void SkillsBlock_Absent_DefaultsToEmbedded()
    {
        var cfg = Load("""
            projects: {}
            secrets: {}
            """);

        cfg.Skills.Source.Should().Be(SkillsSourceMode.Embedded,
            "an unconfigured install runs from the catalog baked into the binary");
    }

    [Fact]
    public void SkillsBlock_PathOnly_InfersPathSource()
    {
        var cfg = Load("""
            projects: {}
            skills:
              path: /work/agent-smith-skills
            secrets: {}
            """);

        cfg.Skills.Source.Should().Be(SkillsSourceMode.Path,
            "the skills-development path override must keep working without an explicit source");
    }

    [Fact]
    public void SkillsBlock_UrlOnly_InfersUrlSource()
    {
        var cfg = Load("""
            projects: {}
            skills:
              url: https://mirror.example.com/skills.tar.gz
            secrets: {}
            """);

        cfg.Skills.Source.Should().Be(SkillsSourceMode.Url);
    }

    [Fact]
    public void SkillsBlock_VersionOnly_KeepsDefaultFetchSource()
    {
        var cfg = Load("""
            projects: {}
            skills:
              version: v3.19.0
            secrets: {}
            """);

        cfg.Skills.Source.Should().Be(SkillsSourceMode.Default,
            "an explicit version pin keeps the release-fetch behavior unchanged");
    }

    [Fact]
    public void SkillsBlock_ExplicitEmbeddedSource_Parses()
    {
        var cfg = Load("""
            projects: {}
            skills:
              source: embedded
            secrets: {}
            """);

        cfg.Skills.Source.Should().Be(SkillsSourceMode.Embedded);
    }

    [Fact]
    public void SkillsBlock_CacheDir_RequiresSnakeCase()
    {
        var cfg = Load("""
            projects: {}
            skills:
              source: default
              version: v1.0.0
              cache_dir: /custom/path
            secrets: {}
            """);

        cfg.Skills.CacheDir.Should().Be("/custom/path");
    }
}
