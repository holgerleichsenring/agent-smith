using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Persistence.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: pocs.archive binds from agentsmith.yml, its absence reads as the defaults, and its rule
/// refuses zero, negative, a path wider than the store's column and a share over 100 — naming the key and the value.</summary>
public sealed class PocArchiveLimitsTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"agentsmith-poc-archive-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void Load_NoArchiveBlock_ReadsTheDefaults_ABlockBindsItsKeys()
    {
        var defaults = Load("projects: {}\n").Pocs.Archive;
        (defaults.MaxZipMb, defaults.MaxUnpackedMb, defaults.MaxEntries, defaults.MaxRatio, defaults.MaxPathChars)
            .Should().Be((25, 100, 2000, 100, 240));
        (defaults.MaxFileMb, defaults.MaxBinaryMb, defaults.MaxDockerfileKb, defaults.MismatchRatioPercent, defaults.MismatchFileLimit)
            .Should().Be((10, 20, 64, 50, 100));

        var bound = Load("projects: {}\npocs:\n  archive:\n    max_zip_mb: 30\n    mismatch_file_limit: 7\n").Pocs.Archive;
        (bound.MaxZipMb, bound.MismatchFileLimit, bound.MaxEntries).Should().Be((30, 7, 2000));
    }

    [Fact]
    public void Problems_TheDefaults_Pass() => new PocArchiveLimits().Problems().Should().BeEmpty();

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Problems_ZeroOrNegative_IsRefusedNamingEveryKey(int value)
    {
        var limits = new PocArchiveLimits
        {
            MaxZipMb = value, MaxUnpackedMb = value, MaxEntries = value, MaxRatio = value, MaxPathChars = value,
            MaxFileMb = value, MaxBinaryMb = value, MaxDockerfileKb = value, MismatchRatioPercent = value,
            MismatchFileLimit = value,
        };

        var problems = limits.Problems().ToList();

        problems.Should().HaveCount(10).And.OnlyContain(p => p.Contains($" is {value}; it must be 1 or more, because"));
        problems.Select(p => p[..p.IndexOf(' ')]).Should().Equal("pocs.archive.max_zip_mb", "pocs.archive.max_unpacked_mb",
            "pocs.archive.max_entries", "pocs.archive.max_ratio", "pocs.archive.max_path_chars", "pocs.archive.max_file_mb",
            "pocs.archive.max_binary_mb", "pocs.archive.max_dockerfile_kb", "pocs.archive.mismatch_ratio_percent",
            "pocs.archive.mismatch_file_limit");
    }

    [Fact]
    public void Problems_APathWiderThanTheColumnOrAShareOver100_IsRefused()
    {
        var problems = new PocArchiveLimits { MaxPathChars = 241, MismatchRatioPercent = 101 }.Problems().ToList();

        problems.Should().HaveCount(2);
        problems[0].Should().StartWith("pocs.archive.max_path_chars is 241; it must be at most 240");
        problems[1].Should().StartWith("pocs.archive.mismatch_ratio_percent is 101; it must be at most 100");
    }

    [Fact]
    public void Problems_ThePocsBlock_CarriesTheArchiveProblems()
    {
        new PocsConfig { Archive = new PocArchiveLimits { MaxEntries = 0 } }.Problems().Should().ContainSingle()
            .Which.Should().StartWith("pocs.archive.max_entries is 0");
    }

    [Fact]
    public void MaxPathChars_TheContractsBound_IsTheStoresColumn()
    {
        ReferenceSetLimits.MaxPathChars.Should().Be(PersistenceLimits.ReferencePath);
    }

    [Fact]
    public void Megabytes_IsBytes() => PocArchiveLimits.Megabytes(2).Should().Be(2L * 1024 * 1024);

    private AgentSmithConfig Load(string yaml)
    {
        File.WriteAllText(_tempFile, yaml);
        return new YamlConfigurationLoader(new RawConfigMaterializer(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(),
            new DeploymentDefaultsApplier(), new ConfigCatalogResolver(), new AgentSmithPaths()), new NoOpSystemEventPublisher())
            .LoadConfig(_tempFile);
    }
}
