using AgentSmith.Contracts.Decisions;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public sealed class FileDecisionLoggerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _agentDir;
    private readonly FileDecisionLogger _sut;

    public FileDecisionLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-decisions-" + Guid.NewGuid().ToString("N")[..8]);
        _agentDir = Path.Combine(_tempDir, ".agentsmith");
        Directory.CreateDirectory(_agentDir);
        _sut = new FileDecisionLogger(NullLogger<FileDecisionLogger>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task LogAsync_NewFile_CreatesFileWithHeaderAndDecision()
    {
        await _sut.LogAsync(_tempDir, DecisionCategory.Architecture,
            "**Redis Streams**: fan-out to multiple consumers required");

        var content = await File.ReadAllTextAsync(Path.Combine(_agentDir, "decisions.md"));
        content.Should().StartWith("# Decision Log");
        content.Should().Contain("## Architecture");
        content.Should().Contain("- **Redis Streams**: fan-out to multiple consumers required");
    }

    [Fact]
    public async Task LogAsync_ExistingSection_AppendsToSection()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_agentDir, "decisions.md"),
            "# Decision Log\n\n## Architecture\n- **First decision**: reason\n");

        await _sut.LogAsync(_tempDir, DecisionCategory.Architecture,
            "**Second decision**: another reason");

        var content = await File.ReadAllTextAsync(Path.Combine(_agentDir, "decisions.md"));
        content.Should().Contain("- **First decision**: reason");
        content.Should().Contain("- **Second decision**: another reason");

        var archCount = content.Split("## Architecture").Length - 1;
        archCount.Should().Be(1, "section header should not be duplicated");
    }

    [Fact]
    public async Task LogAsync_NewCategory_CreatesNewSection()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_agentDir, "decisions.md"),
            "# Decision Log\n\n## Architecture\n- **Existing**: reason\n");

        await _sut.LogAsync(_tempDir, DecisionCategory.Tooling,
            "**DuckDB**: reads Parquet natively");

        var content = await File.ReadAllTextAsync(Path.Combine(_agentDir, "decisions.md"));
        content.Should().Contain("## Architecture");
        content.Should().Contain("## Tooling");
        content.Should().Contain("- **DuckDB**: reads Parquet natively");
    }

    [Fact]
    public async Task LogAsync_MultipleSections_InsertsInCorrectSection()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_agentDir, "decisions.md"),
            "# Decision Log\n\n## Architecture\n- **First**: reason\n\n## Tooling\n- **Tool1**: reason\n");

        await _sut.LogAsync(_tempDir, DecisionCategory.Architecture,
            "**Second**: another reason");

        var content = await File.ReadAllTextAsync(Path.Combine(_agentDir, "decisions.md"));
        var archIndex = content.IndexOf("## Architecture", StringComparison.Ordinal);
        var toolIndex = content.IndexOf("## Tooling", StringComparison.Ordinal);
        var secondIndex = content.IndexOf("**Second**", StringComparison.Ordinal);

        secondIndex.Should().BeGreaterThan(archIndex);
        secondIndex.Should().BeLessThan(toolIndex);
    }

    [Fact]
    public async Task LogAsync_NoAgentSmithDir_CreatesDirectory()
    {
        var freshDir = Path.Combine(Path.GetTempPath(), "agentsmith-nodirtest-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(freshDir);

            await _sut.LogAsync(freshDir, DecisionCategory.Implementation,
                "**Sealed classes**: prevents accidental inheritance");

            File.Exists(Path.Combine(freshDir, ".agentsmith", "decisions.md")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(freshDir))
                Directory.Delete(freshDir, recursive: true);
        }
    }

    [Fact]
    public async Task LogAsync_ConcurrentWrites_AllDecisionsPresent()
    {
        var tasks = Enumerable.Range(1, 10)
            .Select(i => _sut.LogAsync(_tempDir, DecisionCategory.Implementation,
                $"**Decision{i}**: reason {i}"))
            .ToArray();

        await Task.WhenAll(tasks);

        var content = await File.ReadAllTextAsync(Path.Combine(_agentDir, "decisions.md"));
        for (var i = 1; i <= 10; i++)
            content.Should().Contain($"**Decision{i}**: reason {i}");
    }

    [Fact]
    public async Task LogAsync_NullRepoPath_SkipsFileWrite()
    {
        await _sut.LogAsync(null, DecisionCategory.Architecture, "**Test**: reason");

        Directory.GetFiles(_agentDir).Should().BeEmpty();
    }

    [Fact]
    public async Task LogAsync_EmptyRepoPath_SkipsFileWrite()
    {
        await _sut.LogAsync("", DecisionCategory.Architecture, "**Test**: reason");

        Directory.GetFiles(_agentDir).Should().BeEmpty();
    }

    [Fact]
    public void InsertUnderSection_AppendsBeforeNextSection()
    {
        var content = "# Decision Log\n\n## Architecture\n- **First**: reason\n\n## Tooling\n- **Tool**: reason\n";
        var result = FileDecisionLogger.InsertUnderSection(content, "## Architecture", "- **New**: appended");

        var archIndex = result.IndexOf("## Architecture", StringComparison.Ordinal);
        var newIndex = result.IndexOf("- **New**: appended", StringComparison.Ordinal);
        var toolIndex = result.IndexOf("## Tooling", StringComparison.Ordinal);

        newIndex.Should().BeGreaterThan(archIndex);
        newIndex.Should().BeLessThan(toolIndex);
    }

    [Fact]
    public void InsertUnderSection_AppendsAtEndWhenLastSection()
    {
        var content = "# Decision Log\n\n## Architecture\n- **First**: reason\n";
        var result = FileDecisionLogger.InsertUnderSection(content, "## Architecture", "- **New**: appended");

        result.Should().Contain("- **First**: reason");
        result.Should().Contain("- **New**: appended");
    }
}
