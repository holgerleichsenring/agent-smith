using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933da: the pocs block binds from agentsmith.yml, off by default, and states its rule once — each problem a
/// sentence naming the key, the value and the reason, read by the load as a Blocking finding with Field 'pocs'.
/// </summary>
public sealed class PocsConfigTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"agentsmith-pocs-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void Load_NoBlock_IsOffAndValid_ABlockBindsEveryField()
    {
        Load("projects: {}\n").Pocs.Enabled.Should().BeFalse();

        var pocs = Load("projects: {}\npocs:\n  enabled: true\n  project_agent: a\n  project_tracker: t\n  targets:\n"
            + "    - name: main\n      source_repo: gh/poc-src\n      domain_pattern: \"{app}.poc.example.internal\"\n"
            + "      reminder_days: [10, 2]\n      per_person_limit: 5\n").Pocs;

        pocs.ProjectAgent.Should().Be("a");
        var target = pocs.Targets.Should().ContainSingle().Subject;
        (target.SourceRepo, target.Root, target.DeployRoot, target.TemplatePath).Should().Be(("gh/poc-src", "pocs", "deploy", "poc-template"));
        (target.LifetimeDays, target.PerPersonLimit).Should().Be((90, 5));
        target.ReminderDays.Should().Equal(10, 2);
    }

    [Fact]
    public void Problems_ASoundSingleTarget_Passes() => Sound().Problems().Should().BeEmpty();

    [Fact]
    public void Problems_AGlobSourceRepo_IsRefusedNamingTheKeyAndTheValue()
    {
        var pocs = Sound();
        pocs.Targets[0].SourceRepo = "gh/poc-*";

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("pocs.targets[].source_repo").And.Contain("'gh/poc-*'");
    }

    [Theory]
    [InlineData("Main_1", null, "pocs.targets[].name is 'Main_1'")]
    [InlineData("main", "", "source_repo of target 'main' is empty")]
    public void Problems_ABadNameOrEmptyRepo_IsRefusedNamingIt(string name, string? repo, string expected)
    {
        var pocs = Sound();
        pocs.Targets[0].Name = name;
        if (repo is not null) pocs.Targets[0].SourceRepo = repo;

        pocs.Problems().Should().ContainSingle().Which.Should().Contain(expected);
    }

    [Theory]
    [InlineData("/abs", "root of target 'main' is '/abs'")]
    [InlineData("a/../b", "root of target 'main' is 'a/../b'")]
    [InlineData("a\\b", "root of target 'main' is 'a\\b'")]
    [InlineData("a//b", "root of target 'main' is 'a//b'")]
    [InlineData("deploy/pocs", "root of target 'main' ('deploy/pocs') lies inside deploy_root ('deploy')")]
    public void Problems_ABadFolder_IsRefusedNamingKeyAndValue(string root, string expected)
    {
        var pocs = Sound();
        pocs.Targets[0].Root = root;

        pocs.Problems().Should().ContainSingle().Which.Should().Contain(expected);
    }

    [Theory]
    [InlineData("poc.example.internal", "domain_pattern of target 'main' is 'poc.example.internal'")]
    [InlineData("{app}.{app}.example", "exactly one {app}")]
    [InlineData("{app} .example", "domain_pattern")]
    public void Problems_ABadDomainPattern_IsRefused(string pattern, string expected)
    {
        var pocs = Sound();
        pocs.Targets[0].DomainPattern = pattern;

        pocs.Problems().Should().ContainSingle().Which.Should().Contain(expected);
    }

    [Fact]
    public void Problems_LifetimeRemindersAndLimit_OutOfRange_EachRefusedNamingKeyAndValue()
    {
        var pocs = Sound();
        pocs.Targets[0].LifetimeDays = 10;
        pocs.Targets[0].ReminderDays = [10, 0, 3];
        pocs.Targets[0].PerPersonLimit = 0;

        pocs.Problems().Should().HaveCount(3).And.Contain(p => p.Contains("reminder_days of target 'main' holds 10"))
            .And.Contain(p => p.Contains("reminder_days of target 'main' holds 0")).And.Contain(p => p.Contains("per_person_limit of target 'main' is 0"));
        new PocsConfig { Targets = [new PocTargetConfig { Name = "x", SourceRepo = "r", LifetimeDays = 0, ReminderDays = [], Registry = SoundRegistry() }] }
            .Problems().Should().ContainSingle().Which.Should().Contain("lifetime_days of target 'x' is 0");
    }

    [Fact]
    public void Problems_EnabledWithoutTargetAgentOrTracker_IsRefused_OffIsNot()
    {
        var empty = new PocsConfig { Enabled = true };

        // 2026-11-23-933dg: the scanner images have defaults, so an empty scan block is no problem.
        empty.Problems().Should().HaveCount(3).And.Contain(p => p.StartsWith("pocs.targets is empty"))
            .And.Contain(p => p.StartsWith("pocs.project_agent is empty")).And.Contain(p => p.StartsWith("pocs.project_tracker is empty"));
        new PocsConfig().Problems().Should().BeEmpty("an off block may be filled in before the switch is");
    }

    [Fact]
    public void Problems_DuplicateTargetNames_AreRefused()
    {
        var pocs = Sound();
        pocs.Targets.Add(new PocTargetConfig { Name = "MAIN", SourceRepo = "gh/other" });

        pocs.Problems().Should().Contain(p => p.Contains("pocs.targets[].name 'main' is used 2 times"));
    }

    [Fact]
    public void Validator_ABrokenBlock_IsABlockingFindingOnPocs()
    {
        var config = new AgentSmithConfig { Pocs = new PocsConfig { Enabled = true } };

        PocsConfigValidator.Findings(config).Should().HaveCount(3).And.OnlyContain(f => f.IsBlocking && f.Field == "pocs");
        new AgentSmithConfigValidator(new SkillsConfigValidator()).Validate(config).Should().Contain(r => r.Contains("pocs.project_agent"));
    }

    internal static PocsConfig Sound() => new()
    {
        Enabled = true, ProjectAgent = "claude", ProjectTracker = "issues",
        Targets =
        [
            new PocTargetConfig
            {
                Name = "main", SourceRepo = "gh/poc-src", DomainPattern = "{app}.poc.example.internal", Registry = SoundRegistry(),
            },
        ],
    };

    /// <summary>2026-11-23-933de: a registry block every target needs.</summary>
    internal static PocRegistryConfig SoundRegistry() => new()
    {
        LoginServer = "pocregistry.azurecr.io", PullUser = "poc-pull", PullAuth = "poc_pull_token",
    };

    /// <summary>2026-11-23-933dd: a Trivy image pinned by digest — 2026-11-23-933dg: an own mirror, since the key has a
    /// default.</summary>
    internal const string PinnedTrivyImage = "aquasec/trivy@sha256:0000000000000000000000000000000000000000000000000000000000000933";

    internal const string PinnedOsvImage =
        "ghcr.io/google/osv-scanner@sha256:f7ba4be68bac8086b1f88fd598fdca1ca67239c79ad2c2b5c78e03a82e5187c4";

    private AgentSmithConfig Load(string yaml)
    {
        File.WriteAllText(_tempFile, yaml);
        return LoadFile(_tempFile);
    }

    internal static AgentSmithConfig LoadFile(string path) =>
        new YamlConfigurationLoader(new RawConfigMaterializer(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(),
            new DeploymentDefaultsApplier(), new ConfigCatalogResolver(), new AgentSmithPaths()), new NoOpSystemEventPublisher())
            .LoadConfig(path);
}
