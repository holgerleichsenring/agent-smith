using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class CompileKnowledgeHandlerTests : IDisposable
{
    private readonly Mock<ILlmClient> _llmClient = new();
    private readonly CompileKnowledgeHandler _sut;
    private readonly string _tempDir;

    public CompileKnowledgeHandlerTests()
    {
        var prompts = new FakePromptCatalog().WithPrompt("knowledge-system", "knowledge system");
        _sut = new CompileKnowledgeHandler(
            _llmClient.Object,
            new KnowledgePromptBuilder(prompts),
            NullLogger<CompileKnowledgeHandler>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-wiki-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesWikiDirectory()
    {
        SetupRuns("r01-add-feature", "r02-fix-bug");
        SetupLlmResponse();

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Directory.Exists(Path.Combine(_tempDir, ".agentsmith", "wiki")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_DetectsNewRuns()
    {
        SetupRuns("r01-add-feature", "r02-fix-bug");
        SetupLlmResponse();

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("2 run(s)");
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWhenUpToDate()
    {
        SetupRuns("r01-add-feature");
        var wikiDir = Path.Combine(_tempDir, ".agentsmith", "wiki");
        Directory.CreateDirectory(wikiDir);
        File.WriteAllText(Path.Combine(wikiDir, ".last-compiled"), "1");

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Wiki up to date");
        _llmClient.Verify(
            x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TaskType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WritesLastCompiled()
    {
        SetupRuns("r01-add-feature", "r03-refactor");
        SetupLlmResponse();

        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        var lastCompiled = File.ReadAllText(Path.Combine(_tempDir, ".agentsmith", "wiki", ".last-compiled"));
        lastCompiled.Trim().Should().Be("3");
    }

    [Fact]
    public async Task ExecuteAsync_OnlyCompilesNewRuns()
    {
        SetupRuns("r01-add-feature", "r02-fix-bug", "r03-refactor");
        var wikiDir = Path.Combine(_tempDir, ".agentsmith", "wiki");
        Directory.CreateDirectory(wikiDir);
        File.WriteAllText(Path.Combine(wikiDir, ".last-compiled"), "1");
        SetupLlmResponse();

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("2 run(s)");
        result.Message.Should().Contain("r03");
    }

    [Fact]
    public async Task ExecuteAsync_FullRecompile_IgnoresLastCompiled()
    {
        SetupRuns("r01-add-feature");
        var wikiDir = Path.Combine(_tempDir, ".agentsmith", "wiki");
        Directory.CreateDirectory(wikiDir);
        File.WriteAllText(Path.Combine(wikiDir, ".last-compiled"), "1");
        SetupLlmResponse();

        var context = CreateContext(fullRecompile: true);
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("1 run(s)");
    }

    [Fact]
    public async Task ExecuteAsync_NoRunsDirectory_ReturnsOk()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".agentsmith"));

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No runs directory");
    }

    [Fact]
    public async Task ExecuteAsync_WritesWikiFiles()
    {
        SetupRuns("r01-add-feature");
        _llmClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"wiki_updates": {"index.md": "# Wiki Index\n- [Decisions](decisions.md)", "decisions.md": "# Decisions\n- Used Clean Architecture"}}""",
                100, 200));

        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        var wikiDir = Path.Combine(_tempDir, ".agentsmith", "wiki");
        File.Exists(Path.Combine(wikiDir, "index.md")).Should().BeTrue();
        File.Exists(Path.Combine(wikiDir, "decisions.md")).Should().BeTrue();

        var index = File.ReadAllText(Path.Combine(wikiDir, "index.md"));
        index.Should().Contain("Wiki Index");
    }

    [Fact]
    public void GetRunDirectories_ParsesRunNumbers()
    {
        var runsDir = Path.Combine(_tempDir, "runs");
        Directory.CreateDirectory(runsDir);
        Directory.CreateDirectory(Path.Combine(runsDir, "r01-first"));
        Directory.CreateDirectory(Path.Combine(runsDir, "r02-second"));
        Directory.CreateDirectory(Path.Combine(runsDir, "not-a-run"));

        var result = RunDirectoryReader.GetRunDirectories(runsDir);

        result.Should().HaveCount(2);
        result[0].RunNumber.Should().Be(1);
        result[1].RunNumber.Should().Be(2);
    }

    [Fact]
    public void ReadLastCompiled_NoFile_Returns0()
    {
        var result = RunDirectoryReader.ReadLastCompiled(Path.Combine(_tempDir, "nonexistent"));
        result.Should().Be(0);
    }

    [Fact]
    public void ReadLastCompiled_WithValue_ReturnsParsedNumber()
    {
        var wikiDir = Path.Combine(_tempDir, "wiki");
        Directory.CreateDirectory(wikiDir);
        File.WriteAllText(Path.Combine(wikiDir, ".last-compiled"), "5");

        var result = RunDirectoryReader.ReadLastCompiled(wikiDir);
        result.Should().Be(5);
    }

    [Fact]
    public void ParseWikiUpdates_ValidJson_ReturnsDictionary()
    {
        var json = """{"wiki_updates": {"index.md": "# Index", "patterns.md": "# Patterns"}}""";

        var result = WikiUpdateParser.Parse(json);

        result.Should().HaveCount(2);
        result["index.md"].Should().Be("# Index");
        result["patterns.md"].Should().Be("# Patterns");
    }

    [Fact]
    public void ParseWikiUpdates_JsonInMarkdownFences_ParsesCorrectly()
    {
        var json = """
            ```json
            {"wiki_updates": {"index.md": "# Index"}}
            ```
            """;

        var result = WikiUpdateParser.Parse(json);
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ParseWikiUpdates_InvalidJson_ReturnsEmpty()
    {
        var result = WikiUpdateParser.Parse("not json at all");
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildUserPrompt_IncludesExistingWikiAndRuns()
    {
        var runs = new List<RunDirectoryReader.RunData>
        {
            new(1, "r01-feature", "## Plan\nAdd auth", "## Result\nSuccess"),
        };

        var prompt = new KnowledgePromptBuilder(new FakePromptCatalog()).BuildUserPrompt("# Existing Wiki", runs);

        prompt.Should().Contain("Existing Wiki");
        prompt.Should().Contain("r01");
        prompt.Should().Contain("Add auth");
        prompt.Should().Contain("Success");
    }

    private void SetupRuns(params string[] runNames)
    {
        var runsDir = Path.Combine(_tempDir, ".agentsmith", "runs");
        Directory.CreateDirectory(runsDir);

        foreach (var name in runNames)
        {
            var runDir = Path.Combine(runsDir, name);
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, "plan.md"), $"# Plan for {name}\nTest plan content");
            File.WriteAllText(Path.Combine(runDir, "result.md"), $"# Result for {name}\nTest result content");
        }
    }

    private void SetupLlmResponse()
    {
        _llmClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse(
                """{"wiki_updates": {"index.md": "# Project Wiki\nCompiled from runs."}}""",
                100, 200));
    }

    private CompileKnowledgeContext CreateContext(bool fullRecompile = false)
    {
        var repo = new Repository(_tempDir, new BranchName("main"), "https://github.com/test/test");
        return new CompileKnowledgeContext(repo, fullRecompile, new PipelineContext());
    }
}
