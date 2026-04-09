using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class QueryKnowledgeHandlerTests : IDisposable
{
    private readonly Mock<ILlmClient> _llmClient = new();
    private readonly QueryKnowledgeHandler _sut;
    private readonly string _tempDir;

    public QueryKnowledgeHandlerTests()
    {
        _sut = new QueryKnowledgeHandler(
            _llmClient.Object,
            NullLogger<QueryKnowledgeHandler>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-query-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_NoWiki_ReturnsNoWikiMessage()
    {
        var wikiPath = Path.Combine(_tempDir, "wiki");
        var pipeline = new PipelineContext();
        var context = new QueryKnowledgeContext("What is the architecture?", wikiPath, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No wiki found");
        pipeline.TryGet<string>(ContextKeys.QueryAnswer, out var answer).Should().BeTrue();
        answer.Should().Contain("compile-wiki");
    }

    [Fact]
    public async Task ExecuteAsync_WikiExists_QueriesLlm()
    {
        var wikiPath = SetupWiki();
        _llmClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("The project uses Clean Architecture pattern.", 100, 50));

        var pipeline = new PipelineContext();
        var context = new QueryKnowledgeContext("What architecture is used?", wikiPath, pipeline);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.QueryAnswer, out var answer).Should().BeTrue();
        answer.Should().Contain("Clean Architecture");
    }

    [Fact]
    public async Task ExecuteAsync_SetsQueryAnswerInPipeline()
    {
        var wikiPath = SetupWiki();
        _llmClient
            .Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TaskType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("Answer text here.", 100, 50));

        var pipeline = new PipelineContext();
        var context = new QueryKnowledgeContext("question", wikiPath, pipeline);

        await _sut.ExecuteAsync(context, CancellationToken.None);

        pipeline.TryGet<string>(ContextKeys.QueryAnswer, out var answer).Should().BeTrue();
        answer.Should().Contain("Answer text here");
        answer.Should().Contain("Source:");
    }

    [Fact]
    public async Task ReadRelevantWikiFilesAsync_IncludesCoreFiles()
    {
        var wikiPath = SetupWiki();
        File.WriteAllText(Path.Combine(wikiPath, "decisions.md"), "# Decisions");
        File.WriteAllText(Path.Combine(wikiPath, "known-issues.md"), "# Known Issues");
        File.WriteAllText(Path.Combine(wikiPath, "patterns.md"), "# Patterns");

        var result = await QueryKnowledgeHandler.ReadRelevantWikiFilesAsync(
            wikiPath, "anything", CancellationToken.None);

        result.Should().ContainKey("index.md");
        result.Should().ContainKey("decisions.md");
        result.Should().ContainKey("known-issues.md");
        result.Should().ContainKey("patterns.md");
    }

    [Fact]
    public void BuildUserPrompt_IncludesQuestionAndContent()
    {
        var content = new Dictionary<string, string>
        {
            ["index.md"] = "# Index",
            ["patterns.md"] = "# Patterns\n- Use sealed classes",
        };

        var prompt = QueryKnowledgeHandler.BuildUserPrompt("What patterns exist?", content);

        prompt.Should().Contain("What patterns exist?");
        prompt.Should().Contain("sealed classes");
        prompt.Should().Contain("index.md");
    }

    private string SetupWiki()
    {
        var wikiPath = Path.Combine(_tempDir, "wiki");
        Directory.CreateDirectory(wikiPath);
        File.WriteAllText(Path.Combine(wikiPath, "index.md"), "# Project Wiki\n- [Decisions](decisions.md)");
        return wikiPath;
    }
}
