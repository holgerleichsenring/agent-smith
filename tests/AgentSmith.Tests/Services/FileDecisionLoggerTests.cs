using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0380: FileDecisionLogger writes per-phase / per-run YAML files under
/// .agentsmith/decisions/ (decision.schema.json — the IDE format), retiring
/// the legacy .agentsmith/decisions.md append.
/// </summary>
public sealed class FileDecisionLoggerTests : IDisposable
{
    private const string SampleRunId = "2026-05-20T22-27-43-8a3f";

    private readonly string _tempDir;
    private readonly string _decisionsDir;
    private readonly AsyncLocalRunContextAccessor _runContext = new();
    private readonly FileDecisionLogger _sut;

    public FileDecisionLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-decisions-" + Guid.NewGuid().ToString("N")[..8]);
        _decisionsDir = Path.Combine(_tempDir, ".agentsmith", "decisions");
        Directory.CreateDirectory(_tempDir);
        _sut = new FileDecisionLogger(
            TestHelpers.EventTestStubs.NoOp, _runContext,
            new DecisionEventMirror(TestHelpers.EventTestStubs.NoOp, _runContext),
            NullLogger<FileDecisionLogger>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task DecisionLogger_WritesYamlPath_NoLegacyDecisionsMdAppend()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_tempDir, DecisionCategory.Architecture,
            "Redis Streams: fan-out to multiple consumers required");

        File.Exists(Path.Combine(_tempDir, ".agentsmith", "decisions.md"))
            .Should().BeFalse("the legacy p0100 append log is retired");
        var yaml = await File.ReadAllTextAsync(Path.Combine(_decisionsDir, $"{SampleRunId}.yaml"));
        yaml.Should().StartWith($"run: {SampleRunId}");
        yaml.Should().Contain("decisions:");
        yaml.Should().Contain("- category: Architecture");
        yaml.Should().Contain("Redis Streams: fan-out to multiple consumers required");
    }

    [Fact]
    public async Task RunDecisions_PersistAs_RunIdYaml_ParallelToPhaseDecisions()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_tempDir, DecisionCategory.Implementation, "run-scoped choice");
        await _sut.LogAsync(_tempDir, DecisionCategory.Tooling, "phase-scoped choice",
            sourceLabel: "p0380");

        var runFile = Path.Combine(_decisionsDir, $"{SampleRunId}.yaml");
        var phaseFile = Path.Combine(_decisionsDir, "p0380.yaml");
        File.Exists(runFile).Should().BeTrue("run decisions land in decisions/<runId>.yaml");
        File.Exists(phaseFile).Should().BeTrue("phase decisions stay in decisions/<phase>.yaml");
        (await File.ReadAllTextAsync(runFile)).Should().StartWith($"run: {SampleRunId}");
        (await File.ReadAllTextAsync(phaseFile)).Should().StartWith("phase: p0380");
    }

    [Fact]
    public async Task LogAsync_MultipleDecisions_AppendToOneDecisionsArray()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_tempDir, DecisionCategory.Architecture, "first");
        await _sut.LogAsync(_tempDir, DecisionCategory.TradeOff, "second");

        var yaml = await File.ReadAllTextAsync(Path.Combine(_decisionsDir, $"{SampleRunId}.yaml"));
        yaml.Split("run:").Length.Should().Be(2, "one file per run, decisions array inside");
        yaml.Should().Contain("- category: Architecture");
        yaml.Should().Contain("- category: TradeOff");
    }

    [Fact]
    public async Task LogAsync_DecisionWithNewlinesAndQuotes_StaysOneYamlScalar()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_tempDir, DecisionCategory.Implementation,
            "chose \"X\" over Y\nbecause Z");

        var yaml = await File.ReadAllTextAsync(Path.Combine(_decisionsDir, $"{SampleRunId}.yaml"));
        yaml.Should().Contain("chose: \"chose \\\"X\\\" over Y\\nbecause Z\"");
    }

    [Fact]
    public async Task LogAsync_NoRunScopeAndNoPhaseLabel_WritesNoFile()
    {
        await _sut.LogAsync(_tempDir, DecisionCategory.Architecture, "unscoped");

        Directory.Exists(_decisionsDir).Should().BeFalse(
            "without a phase label or run scope there is no schema-conformant target");
    }

    [Fact]
    public async Task LogAsync_NullRepoPath_SkipsFileWrite()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(null, DecisionCategory.Architecture, "test");

        Directory.Exists(_decisionsDir).Should().BeFalse();
    }

    [Fact]
    public async Task LogAsync_ConcurrentWrites_AllDecisionsPresent()
    {
        using var scope = _runContext.BeginScope(SampleRunId);

        var tasks = Enumerable.Range(1, 10)
            .Select(i => _sut.LogAsync(_tempDir, DecisionCategory.Implementation, $"decision {i}"))
            .ToArray();
        await Task.WhenAll(tasks);

        var yaml = await File.ReadAllTextAsync(Path.Combine(_decisionsDir, $"{SampleRunId}.yaml"));
        for (var i = 1; i <= 10; i++)
            yaml.Should().Contain($"decision {i}");
    }

    [Fact]
    public async Task LogAsync_TicketHashLabel_IsNotAPhase_FallsBackToRunFile()
    {
        // GeneratePlanHandler passes "#42" as sourceLabel — not a phase id, so
        // the decision belongs to the run's YAML.
        using var scope = _runContext.BeginScope(SampleRunId);

        await _sut.LogAsync(_tempDir, DecisionCategory.Implementation, "ticket-labelled", sourceLabel: "#42");

        File.Exists(Path.Combine(_decisionsDir, $"{SampleRunId}.yaml")).Should().BeTrue();
        File.Exists(Path.Combine(_decisionsDir, "#42.yaml")).Should().BeFalse();
    }
}
