using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class SecurityTrendHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_NoRepository_ReturnsOk()
    {
        var sut = MakeHandler(NewEmptyReader().Object);
        var pipeline = new PipelineContext();
        var context = new SecurityTrendContext(pipeline);

        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No repository");
    }

    [Fact]
    public async Task ExecuteAsync_WithRepository_StoresTrendInPipeline()
    {
        var sut = MakeHandler(NewEmptyReader().Object);
        var pipeline = NewPipelineWithRepo();

        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("CRITICAL", "src/auth.cs", 10, "Hardcoded password", "Found hardcoded password", 90),
            ObservationFactory.Make("HIGH", "src/db.cs", 20, "SQL injection", "Possible SQL injection", 80),
            ObservationFactory.Make("MEDIUM", "src/log.cs", 5, "Info leak", "Information leakage", 60)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);

        var context = new SecurityTrendContext(pipeline);
        var result = await sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<SecurityTrend>(ContextKeys.SecurityTrend, out var trend).Should().BeTrue();
        trend.Should().NotBeNull();
        // p0123: Critical maps to High in ObservationSeverity (no Critical value)
        trend!.Current.FindingsCritical.Should().Be(0);
        trend.Current.FindingsHigh.Should().Be(2);
        trend.Current.FindingsMedium.Should().Be(1);
        trend.Current.FindingsRetained.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithPreviousSnapshot_CalculatesDeltas()
    {
        var previousYaml = """
            date: 2026-04-01T10:00:00Z
            branch: main
            findings_critical: 3
            findings_high: 5
            findings_medium: 10
            findings_retained: 18
            findings_auto_fixed: 2
            scan_types:
              - StaticPatternScan
            new_since_last: 0
            resolved_since_last: 0
            top_categories:
              - Hardcoded
            cost_usd: 0.0100
            """;

        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync("/work/.agentsmith/security", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/.agentsmith/security/2026-04-01-main.yaml" });
        reader.Setup(r => r.TryReadAsync("/work/.agentsmith/security/2026-04-01-main.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousYaml);

        var sut = MakeHandler(reader.Object);
        var pipeline = NewPipelineWithRepo();

        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("CRITICAL", "src/auth.cs", 10, "Hardcoded password", "desc", 90),
            ObservationFactory.Make("HIGH", "src/db.cs", 20, "SQL injection", "desc", 80)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);

        var context = new SecurityTrendContext(pipeline);
        await sut.ExecuteAsync(context, CancellationToken.None);

        pipeline.TryGet<SecurityTrend>(ContextKeys.SecurityTrend, out var trend).Should().BeTrue();
        trend!.Previous.Should().NotBeNull();
        // p0123: Previous YAML still carries critical from old runs; current is observation-shaped (no Critical).
        trend.Previous!.FindingsCritical.Should().Be(3);
        trend.CriticalDelta.Should().Be(-3);  // current critical = 0
        trend.HighDelta.Should().Be(-3);      // 1 critical-mapped-to-high + 1 high - 5 previous high
        trend.TotalScans.Should().Be(2);
    }

    [Fact]
    public void CalculateTrend_NoPrevious_AllFindingsAreNew()
    {
        var current = CreateSnapshot(critical: 2, high: 3, medium: 5, retained: 10, cost: 0.05m);

        var trend = SecurityTrendHandler.CalculateTrend(current, null, 0);

        trend.NewFindings.Should().Be(10);
        trend.ResolvedFindings.Should().Be(0);
        trend.CriticalDelta.Should().Be(2);
        trend.HighDelta.Should().Be(3);
        trend.TotalScans.Should().Be(1);
        trend.AverageCost.Should().Be(0.05m);
        trend.Previous.Should().BeNull();
    }

    [Fact]
    public void CalculateTrend_WithPrevious_CalculatesCorrectDeltas()
    {
        var current = CreateSnapshot(critical: 2, high: 3, medium: 5, retained: 10, cost: 0.06m);
        var previous = CreateSnapshot(critical: 4, high: 5, medium: 3, retained: 12, cost: 0.04m);

        var trend = SecurityTrendHandler.CalculateTrend(current, previous, 3);

        trend.CriticalDelta.Should().Be(-2);
        trend.HighDelta.Should().Be(-2);
        trend.TotalScans.Should().Be(4);
        trend.AverageCost.Should().Be(0.045m); // (0.04 * 3 + 0.06) / 4
    }

    [Fact]
    public void CalculateTrend_WithAutoFixed_AccountsForAutoFixed()
    {
        var current = CreateSnapshot(critical: 1, high: 2, medium: 3, retained: 6, autoFixed: 1, cost: 0.05m);
        var previous = CreateSnapshot(critical: 2, high: 3, medium: 4, retained: 9, autoFixed: 0, cost: 0.04m);

        var trend = SecurityTrendHandler.CalculateTrend(current, previous, 1);

        // new = max(0, 6 - 9 + 0) = 0
        trend.NewFindings.Should().Be(0);
        // resolved = max(0, 9 - 6 + 1) = 4
        trend.ResolvedFindings.Should().Be(4);
    }

    [Fact]
    public void CalculateTrend_NewFindingsNeverNegative()
    {
        var current = CreateSnapshot(critical: 0, high: 0, medium: 0, retained: 2, cost: 0.01m);
        var previous = CreateSnapshot(critical: 5, high: 5, medium: 5, retained: 15, cost: 0.01m);

        var trend = SecurityTrendHandler.CalculateTrend(current, previous, 1);

        trend.NewFindings.Should().BeGreaterThanOrEqualTo(0);
        trend.ResolvedFindings.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void BuildCurrentSnapshot_WithObservations_CountsSeverities()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(new BranchName("feature/test"), "https://github.com/test/test");

        // p0123: Critical maps to High (no Critical in framework severity enum)
        var observations = new List<SkillObservation>
        {
            ObservationFactory.Make("CRITICAL", "a.cs", 1, "A critical", "desc", 90),
            ObservationFactory.Make("CRITICAL", "b.cs", 2, "B critical", "desc", 90),
            ObservationFactory.Make("HIGH", "c.cs", 3, "C high", "desc", 80),
            ObservationFactory.Make("MEDIUM", "d.cs", 4, "D medium", "desc", 70),
            ObservationFactory.Make("LOW", "e.cs", 5, "E low", "desc", 50)
        };
        pipeline.Set(ContextKeys.SkillObservations, observations);

        var snapshot = SecuritySnapshotBuilder.BuildCurrentSnapshot(pipeline, repo);

        snapshot.FindingsCritical.Should().Be(0);
        snapshot.FindingsHigh.Should().Be(3);
        snapshot.FindingsMedium.Should().Be(1);
        snapshot.FindingsRetained.Should().Be(5);
        snapshot.Branch.Should().Be("feature/test");
    }

    [Fact]
    public void BuildCurrentSnapshot_NoFindings_ReturnsZeroCounts()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(new BranchName("main"), "https://github.com/test/test");

        var snapshot = SecuritySnapshotBuilder.BuildCurrentSnapshot(pipeline, repo);

        snapshot.FindingsCritical.Should().Be(0);
        snapshot.FindingsHigh.Should().Be(0);
        snapshot.FindingsMedium.Should().Be(0);
        snapshot.FindingsRetained.Should().Be(0);
    }

    [Fact]
    public void BuildCurrentSnapshot_WithScanResults_DeterminesScanTypes()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(new BranchName("main"), "https://github.com/test/test");
        pipeline.Set(ContextKeys.StaticScanResult, new StaticScanResult([], 0, 0, 0));
        pipeline.Set(ContextKeys.DependencyAuditResult, new DependencyAuditResult([], "DOTNET", 0));

        var snapshot = SecuritySnapshotBuilder.BuildCurrentSnapshot(pipeline, repo);

        snapshot.ScanTypes.Should().Contain("StaticPatternScan");
        snapshot.ScanTypes.Should().Contain("DependencyAudit");
        snapshot.ScanTypes.Should().NotContain("GitHistoryScan");
    }

    [Fact]
    public async Task LoadSnapshotsAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var reader = NewEmptyReader();

        var result = await SnapshotYamlParser.LoadSnapshotsAsync(reader.Object, "/work/.agentsmith/security", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadSnapshotsAsync_NonExistentDirectory_ReturnsEmptyList()
    {
        var reader = NewEmptyReader();

        var result = await SnapshotYamlParser.LoadSnapshotsAsync(reader.Object, "/work/.agentsmith/nope", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadSnapshotsAsync_ValidYamlFile_ParsesSnapshot()
    {
        var yaml = """
            date: 2026-04-01T10:00:00Z
            branch: main
            findings_critical: 3
            findings_high: 5
            findings_medium: 8
            findings_retained: 16
            findings_auto_fixed: 1
            scan_types:
              - StaticPatternScan
              - GitHistoryScan
            new_since_last: 2
            resolved_since_last: 3
            top_categories:
              - Hardcoded
              - SQLInjection
            cost_usd: 0.0250
            """;

        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync("/work/sec", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/sec/2026-04-01-main.yaml" });
        reader.Setup(r => r.TryReadAsync("/work/sec/2026-04-01-main.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync(yaml);

        var result = await SnapshotYamlParser.LoadSnapshotsAsync(reader.Object, "/work/sec", CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].FindingsCritical.Should().Be(3);
        result[0].FindingsHigh.Should().Be(5);
        result[0].FindingsMedium.Should().Be(8);
        result[0].FindingsRetained.Should().Be(16);
        result[0].FindingsAutoFixed.Should().Be(1);
        result[0].Branch.Should().Be("main");
        result[0].ScanTypes.Should().BeEquivalentTo(["StaticPatternScan", "GitHistoryScan"]);
        result[0].TopCategories.Should().BeEquivalentTo(["Hardcoded", "SQLInjection"]);
        result[0].CostUsd.Should().Be(0.0250m);
    }

    [Fact]
    public async Task LoadSnapshotsAsync_MalformedFile_SkipsIt()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync("/work/sec", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "/work/sec/bad.yaml", "/work/sec/no-date.yaml" });
        reader.Setup(r => r.TryReadAsync("/work/sec/bad.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("this is not valid yaml at all: [[[");
        reader.Setup(r => r.TryReadAsync("/work/sec/no-date.yaml", It.IsAny<CancellationToken>()))
            .ReturnsAsync("branch: main\nfindings_critical: 1");

        var result = await SnapshotYamlParser.LoadSnapshotsAsync(reader.Object, "/work/sec", CancellationToken.None);

        // The parser requires "date" key; both have no/invalid date
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSnapshotYaml_ValidYaml_ReturnsSnapshot()
    {
        var yaml = """
            date: 2026-03-15T14:30:00Z
            branch: develop
            findings_critical: 1
            findings_high: 2
            findings_medium: 3
            findings_retained: 6
            findings_auto_fixed: 0
            scan_types:
              - Nuclei
            new_since_last: 1
            resolved_since_last: 0
            top_categories:
              - XSS
            cost_usd: 0.0300
            """;

        var result = SnapshotYamlParser.ParseSnapshotYaml(yaml);

        result.Should().NotBeNull();
        result!.Branch.Should().Be("develop");
        result.FindingsCritical.Should().Be(1);
        result.FindingsRetained.Should().Be(6);
        result.ScanTypes.Should().Contain("Nuclei");
        result.CostUsd.Should().Be(0.0300m);
    }

    [Fact]
    public void ParseSnapshotYaml_NoDateKey_ReturnsNull()
    {
        var yaml = "branch: main\nfindings_critical: 1";

        var result = SnapshotYamlParser.ParseSnapshotYaml(yaml);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseSnapshotYaml_EmptyLists_ReturnsEmptyCollections()
    {
        var yaml = """
            date: 2026-04-01T00:00:00Z
            branch: main
            findings_critical: 0
            findings_high: 0
            findings_medium: 0
            findings_retained: 0
            findings_auto_fixed: 0
            scan_types:
            new_since_last: 0
            resolved_since_last: 0
            top_categories:
            cost_usd: 0.0000
            """;

        var result = SnapshotYamlParser.ParseSnapshotYaml(yaml);

        result.Should().NotBeNull();
        result!.ScanTypes.Should().BeEmpty();
        result.TopCategories.Should().BeEmpty();
    }

    private static Mock<ISandboxFileReader> NewEmptyReader()
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        return reader;
    }

    private static SecurityTrendHandler MakeHandler(ISandboxFileReader reader)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader);
        return new SecurityTrendHandler(factory.Object, NullLogger<SecurityTrendHandler>.Instance);
    }

    private static PipelineContext NewPipelineWithRepo()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(new BranchName("main"), "https://github.com/test/test");
        pipeline.Set(ContextKeys.Repository, repo);
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        return pipeline;
    }

    private static SecurityRunSnapshot CreateSnapshot(
        int critical = 0, int high = 0, int medium = 0,
        int retained = 0, int autoFixed = 0, decimal cost = 0m)
    {
        return new SecurityRunSnapshot(
            Date: DateTimeOffset.UtcNow,
            Branch: "main",
            FindingsCritical: critical,
            FindingsHigh: high,
            FindingsMedium: medium,
            FindingsRetained: retained,
            FindingsAutoFixed: autoFixed,
            ScanTypes: ["StaticPatternScan"],
            NewSinceLast: 0,
            ResolvedSinceLast: 0,
            TopCategories: ["Hardcoded"],
            CostUsd: cost);
    }
}
