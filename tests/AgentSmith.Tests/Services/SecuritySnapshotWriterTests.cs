using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class SecuritySnapshotWriterTests
{
    private readonly SecuritySnapshotWriter _sut;
    private readonly Mock<ISandboxFileReader> _readerMock = new();
    private readonly List<(string Path, string Content)> _written = new();

    public SecuritySnapshotWriterTests()
    {
        _readerMock.Setup(r => r.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((p, c, _) => _written.Add((p, c)))
            .Returns(Task.CompletedTask);

        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(_readerMock.Object);

        _sut = new SecuritySnapshotWriter(factory.Object, NullLogger<SecuritySnapshotWriter>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoRepository_ReturnsOk()
    {
        var pipeline = new PipelineContext();
        var context = new SecuritySnapshotWriteContext(pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No repository");
    }

    [Fact]
    public async Task ExecuteAsync_NoTrendData_ReturnsOk()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(new BranchName("main"), "https://github.com/test/test");
        pipeline.Set(ContextKeys.Repository, repo);
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());

        var context = new SecuritySnapshotWriteContext(pipeline);
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No trend data");
    }

    [Fact]
    public async Task ExecuteAsync_WithTrend_WritesSnapshotFile()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(new BranchName("main"), "https://github.com/test/test");
        pipeline.Set(ContextKeys.Repository, repo);
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());

        var snapshot = new SecurityRunSnapshot(
            Date: new DateTimeOffset(2026, 4, 8, 10, 0, 0, TimeSpan.Zero),
            Branch: "main",
            FindingsCritical: 2,
            FindingsHigh: 3,
            FindingsMedium: 5,
            FindingsRetained: 10,
            FindingsAutoFixed: 1,
            ScanTypes: ["StaticPatternScan"],
            NewSinceLast: 2,
            ResolvedSinceLast: 1,
            TopCategories: ["Hardcoded"],
            CostUsd: 0.0150m);

        var trend = new SecurityTrend(2, 1, -1, 0, 3, 0.0150m, null, snapshot);
        pipeline.Set(ContextKeys.SecurityTrend, trend);

        var context = new SecuritySnapshotWriteContext(pipeline);
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        _written.Should().HaveCount(1);
        _written[0].Path.Should().StartWith("/work/.agentsmith/security/").And.EndWith(".yaml");

        var content = _written[0].Content;
        content.Should().Contain("findings_critical: 2");
        content.Should().Contain("findings_high: 3");
        content.Should().Contain("branch: main");
        content.Should().Contain("cost_usd: 0.0150");
    }

    [Fact]
    public void FormatSnapshot_ProducesValidYaml()
    {
        var snapshot = new SecurityRunSnapshot(
            Date: new DateTimeOffset(2026, 4, 8, 14, 30, 0, TimeSpan.Zero),
            Branch: "feature/auth",
            FindingsCritical: 1,
            FindingsHigh: 2,
            FindingsMedium: 3,
            FindingsRetained: 6,
            FindingsAutoFixed: 0,
            ScanTypes: ["StaticPatternScan", "GitHistoryScan"],
            NewSinceLast: 1,
            ResolvedSinceLast: 2,
            TopCategories: ["Hardcoded", "SQLInjection"],
            CostUsd: 0.0250m);

        var yaml = SecuritySnapshotWriter.FormatSnapshot(snapshot);

        yaml.Should().Contain("date: 2026-04-08T14:30:00Z");
        yaml.Should().Contain("branch: feature/auth");
        yaml.Should().Contain("findings_critical: 1");
        yaml.Should().Contain("findings_high: 2");
        yaml.Should().Contain("findings_medium: 3");
        yaml.Should().Contain("findings_retained: 6");
        yaml.Should().Contain("findings_auto_fixed: 0");
        yaml.Should().Contain("  - StaticPatternScan");
        yaml.Should().Contain("  - GitHistoryScan");
        yaml.Should().Contain("new_since_last: 1");
        yaml.Should().Contain("resolved_since_last: 2");
        yaml.Should().Contain("  - Hardcoded");
        yaml.Should().Contain("cost_usd: 0.0250");
    }

    [Fact]
    public void FormatSnapshot_UsesInvariantCulture()
    {
        var snapshot = new SecurityRunSnapshot(
            Date: new DateTimeOffset(2026, 4, 8, 0, 0, 0, TimeSpan.Zero),
            Branch: "main",
            FindingsCritical: 0,
            FindingsHigh: 0,
            FindingsMedium: 0,
            FindingsRetained: 0,
            FindingsAutoFixed: 0,
            ScanTypes: [],
            NewSinceLast: 0,
            ResolvedSinceLast: 0,
            TopCategories: [],
            CostUsd: 1.2345m);

        var yaml = SecuritySnapshotWriter.FormatSnapshot(snapshot);

        yaml.Should().Contain("1.2345");
        yaml.Should().NotContain("1,2345");
    }

    [Theory]
    [InlineData("feature/auth-login", "feature-auth-login")]
    [InlineData("main", "main")]
    [InlineData("release\\1.0", "release-1.0")]
    [InlineData("UPPER BRANCH", "upper-branch")]
    public void SanitizeBranch_SanitizesCorrectly(string input, string expected)
    {
        SecuritySnapshotWriter.SanitizeBranch(input).Should().Be(expected);
    }

    [Fact]
    public void SanitizeBranch_TruncatesLongBranch()
    {
        var longBranch = "feature/this-is-a-very-long-branch-name-that-should-be-truncated-for-filenames";
        var result = SecuritySnapshotWriter.SanitizeBranch(longBranch);

        result.Length.Should().BeLessThanOrEqualTo(40);
        result.Should().NotEndWith("-");
    }

    [Fact]
    public void FormatSnapshot_RoundTrip_ParsesCorrectly()
    {
        var snapshot = new SecurityRunSnapshot(
            Date: new DateTimeOffset(2026, 4, 8, 10, 0, 0, TimeSpan.Zero),
            Branch: "main",
            FindingsCritical: 3,
            FindingsHigh: 5,
            FindingsMedium: 8,
            FindingsRetained: 16,
            FindingsAutoFixed: 2,
            ScanTypes: ["StaticPatternScan", "DependencyAudit"],
            NewSinceLast: 1,
            ResolvedSinceLast: 3,
            TopCategories: ["Hardcoded", "Injection"],
            CostUsd: 0.0450m);

        var yaml = SecuritySnapshotWriter.FormatSnapshot(snapshot);
        var parsed = SnapshotYamlParser.ParseSnapshotYaml(yaml);

        parsed.Should().NotBeNull();
        parsed!.FindingsCritical.Should().Be(3);
        parsed.FindingsHigh.Should().Be(5);
        parsed.FindingsMedium.Should().Be(8);
        parsed.FindingsRetained.Should().Be(16);
        parsed.FindingsAutoFixed.Should().Be(2);
        parsed.Branch.Should().Be("main");
        parsed.ScanTypes.Should().BeEquivalentTo(["StaticPatternScan", "DependencyAudit"]);
        parsed.TopCategories.Should().BeEquivalentTo(["Hardcoded", "Injection"]);
        parsed.CostUsd.Should().Be(0.0450m);
    }
}
