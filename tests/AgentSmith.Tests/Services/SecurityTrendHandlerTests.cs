using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class SecurityTrendHandlerTests : IDisposable
{
    private readonly SecurityTrendHandler _sut;
    private readonly string _tempDir;

    public SecurityTrendHandlerTests()
    {
        _sut = new SecurityTrendHandler(NullLogger<SecurityTrendHandler>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-trend-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_NoRepository_ReturnsOk()
    {
        var pipeline = new PipelineContext();
        var context = new SecurityTrendContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No repository");
    }

    [Fact]
    public async Task ExecuteAsync_WithRepository_StoresTrendInPipeline()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(_tempDir, new BranchName("main"), "https://github.com/test/test");
        pipeline.Set(ContextKeys.Repository, repo);

        var findings = new List<Finding>
        {
            new("CRITICAL", "src/auth.cs", 10, null, "Hardcoded password", "Found hardcoded password", 9),
            new("HIGH", "src/db.cs", 20, null, "SQL injection", "Possible SQL injection", 8),
            new("MEDIUM", "src/log.cs", 5, null, "Info leak", "Information leakage", 6)
        };
        pipeline.Set(ContextKeys.ExtractedFindings, (IReadOnlyList<Finding>)findings.AsReadOnly());

        var context = new SecurityTrendContext(pipeline);
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<SecurityTrend>(ContextKeys.SecurityTrend, out var trend).Should().BeTrue();
        trend.Should().NotBeNull();
        trend!.Current.FindingsCritical.Should().Be(1);
        trend.Current.FindingsHigh.Should().Be(1);
        trend.Current.FindingsMedium.Should().Be(1);
        trend.Current.FindingsRetained.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithPreviousSnapshot_CalculatesDeltas()
    {
        var securityDir = Path.Combine(_tempDir, ".agentsmith", "security");
        Directory.CreateDirectory(securityDir);

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
        File.WriteAllText(Path.Combine(securityDir, "2026-04-01-main.yaml"), previousYaml);

        var pipeline = new PipelineContext();
        var repo = new Repository(_tempDir, new BranchName("main"), "https://github.com/test/test");
        pipeline.Set(ContextKeys.Repository, repo);

        var findings = new List<Finding>
        {
            new("CRITICAL", "src/auth.cs", 10, null, "Hardcoded password", "desc", 9),
            new("HIGH", "src/db.cs", 20, null, "SQL injection", "desc", 8)
        };
        pipeline.Set(ContextKeys.ExtractedFindings, (IReadOnlyList<Finding>)findings.AsReadOnly());

        var context = new SecurityTrendContext(pipeline);
        await _sut.ExecuteAsync(context, CancellationToken.None);

        pipeline.TryGet<SecurityTrend>(ContextKeys.SecurityTrend, out var trend).Should().BeTrue();
        trend!.Previous.Should().NotBeNull();
        trend.Previous!.FindingsCritical.Should().Be(3);
        trend.CriticalDelta.Should().Be(-2); // 1 - 3
        trend.HighDelta.Should().Be(-4);     // 1 - 5
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
    public void BuildCurrentSnapshot_WithFindings_CountsSeverities()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(_tempDir, new BranchName("feature/test"), "https://github.com/test/test");

        var findings = new List<Finding>
        {
            new("CRITICAL", "a.cs", 1, null, "A critical", "desc", 9),
            new("CRITICAL", "b.cs", 2, null, "B critical", "desc", 9),
            new("HIGH", "c.cs", 3, null, "C high", "desc", 8),
            new("MEDIUM", "d.cs", 4, null, "D medium", "desc", 7),
            new("LOW", "e.cs", 5, null, "E low", "desc", 5)
        };
        pipeline.Set(ContextKeys.ExtractedFindings, (IReadOnlyList<Finding>)findings.AsReadOnly());

        var snapshot = SecurityTrendHandler.BuildCurrentSnapshot(pipeline, repo);

        snapshot.FindingsCritical.Should().Be(2);
        snapshot.FindingsHigh.Should().Be(1);
        snapshot.FindingsMedium.Should().Be(1);
        snapshot.FindingsRetained.Should().Be(5);
        snapshot.Branch.Should().Be("feature/test");
    }

    [Fact]
    public void BuildCurrentSnapshot_NoFindings_ReturnsZeroCounts()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(_tempDir, new BranchName("main"), "https://github.com/test/test");

        var snapshot = SecurityTrendHandler.BuildCurrentSnapshot(pipeline, repo);

        snapshot.FindingsCritical.Should().Be(0);
        snapshot.FindingsHigh.Should().Be(0);
        snapshot.FindingsMedium.Should().Be(0);
        snapshot.FindingsRetained.Should().Be(0);
    }

    [Fact]
    public void BuildCurrentSnapshot_WithScanResults_DeterminesScanTypes()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(_tempDir, new BranchName("main"), "https://github.com/test/test");
        pipeline.Set(ContextKeys.StaticScanResult, new StaticScanResult([], 0, 0, 0));
        pipeline.Set(ContextKeys.DependencyAuditResult, new DependencyAuditResult([], "DOTNET", 0));

        var snapshot = SecurityTrendHandler.BuildCurrentSnapshot(pipeline, repo);

        snapshot.ScanTypes.Should().Contain("StaticPatternScan");
        snapshot.ScanTypes.Should().Contain("DependencyAudit");
        snapshot.ScanTypes.Should().NotContain("GitHistoryScan");
    }

    [Fact]
    public void LoadSnapshots_EmptyDirectory_ReturnsEmptyList()
    {
        var dir = Path.Combine(_tempDir, "empty-security");
        Directory.CreateDirectory(dir);

        var result = SecurityTrendHandler.LoadSnapshots(dir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void LoadSnapshots_NonExistentDirectory_ReturnsEmptyList()
    {
        var result = SecurityTrendHandler.LoadSnapshots(Path.Combine(_tempDir, "nonexistent"));

        result.Should().BeEmpty();
    }

    [Fact]
    public void LoadSnapshots_ValidYamlFile_ParsesSnapshot()
    {
        var dir = Path.Combine(_tempDir, "security");
        Directory.CreateDirectory(dir);

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
        File.WriteAllText(Path.Combine(dir, "2026-04-01-main.yaml"), yaml);

        var result = SecurityTrendHandler.LoadSnapshots(dir);

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
    public void LoadSnapshots_MalformedFile_SkipsIt()
    {
        var dir = Path.Combine(_tempDir, "security-bad");
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "bad.yaml"), "this is not valid yaml at all: [[[");
        File.WriteAllText(Path.Combine(dir, "no-date.yaml"), "branch: main\nfindings_critical: 1");

        var result = SecurityTrendHandler.LoadSnapshots(dir);

        // no-date.yaml has no "date:" so returns null; bad.yaml should either parse partially or be skipped
        // The parser requires "date" key to be present
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

        var result = SecurityTrendHandler.ParseSnapshotYaml(yaml);

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

        var result = SecurityTrendHandler.ParseSnapshotYaml(yaml);

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

        var result = SecurityTrendHandler.ParseSnapshotYaml(yaml);

        result.Should().NotBeNull();
        result!.ScanTypes.Should().BeEmpty();
        result.TopCategories.Should().BeEmpty();
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
