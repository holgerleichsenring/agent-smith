using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class ProjectAnalyzerTests
{
    private readonly Mock<IAgenticAnalyzerFactory> _factory = new();
    private readonly Mock<IAgenticAnalyzer> _analyzer = new();
    private readonly Mock<IPromptCatalog> _prompts = new();
    private readonly Mock<IRepositoryToolDispatcher> _dispatcher = new();

    public ProjectAnalyzerTests()
    {
        _factory.Setup(f => f.Create(It.IsAny<AgentConfig>())).Returns(_analyzer.Object);
        _prompts.Setup(p => p.Get("project-analyzer-system")).Returns("system");
    }

    [Fact]
    public async Task AnalyzeAsync_ValidJson_ReturnsParsedProjectMapWithTestProjects()
    {
        const string validJson = """
            {
              "primary_language": "C#",
              "frameworks": [".NET 8"],
              "modules": [{"path": "src/X", "role": "production", "depends_on": []}],
              "test_projects": [{"path": "tests/X.Tests", "framework": "xUnit", "file_count": 42, "sample_test_path": "tests/X.Tests/Sample.cs"}],
              "entry_points": ["src/X/Program.cs"],
              "conventions": {"naming_pattern": "PascalCase"},
              "ci": {"has_ci": true, "build_command": "dotnet build", "test_command": "dotnet test", "ci_system": "GitHub Actions"}
            }
            """;
        StubAnalyzerResponse(validJson);

        var sut = BuildSut();
        var map = await sut.AnalyzeAsync("/repo", new AgentConfig { Type = "claude", Model = "x" }, CancellationToken.None);

        map.PrimaryLanguage.Should().Be("C#");
        map.TestProjects.Should().HaveCount(1);
        map.TestProjects[0].FileCount.Should().Be(42);
        map.TestProjects[0].Framework.Should().Be("xUnit");
        map.Ci.HasCi.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_FirstAttemptMalformedSecondValid_RetriesAndSucceeds()
    {
        const string validJson = "{\"primary_language\": \"Python\"}";
        var responses = new Queue<string>(["{ not valid json", validJson]);

        _analyzer.Setup(a => a.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<ToolDefinition>>(),
                It.IsAny<IToolCallHandler>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AnalysisResult(responses.Dequeue(), 1, 0, new AnalyzerTokenUsage(0, 0)));

        var sut = BuildSut();
        var map = await sut.AnalyzeAsync("/repo", new AgentConfig { Type = "claude", Model = "x" }, CancellationToken.None);

        map.PrimaryLanguage.Should().Be("Python");
        _analyzer.Verify(a => a.AnalyzeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<ToolDefinition>>(),
            It.IsAny<IToolCallHandler>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task AnalyzeAsync_BothAttemptsMalformed_FailsLoud()
    {
        StubAnalyzerResponse("{ broken json");

        var sut = BuildSut();
        var act = async () => await sut.AnalyzeAsync(
            "/repo", new AgentConfig { Type = "claude", Model = "x" }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*failed after 2 attempts*");
    }

    [Fact]
    public async Task AnalyzeAsync_StripsCodeFenceWrappersFromModelOutput()
    {
        const string fenced = """
            ```json
            {"primary_language": "Go"}
            ```
            """;
        StubAnalyzerResponse(fenced);

        var sut = BuildSut();
        var map = await sut.AnalyzeAsync(
            "/repo", new AgentConfig { Type = "claude", Model = "x" }, CancellationToken.None);

        map.PrimaryLanguage.Should().Be("Go");
    }

    private void StubAnalyzerResponse(string finalText) =>
        _analyzer.Setup(a => a.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<ToolDefinition>>(),
                It.IsAny<IToolCallHandler>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalysisResult(finalText, 1, 0, new AnalyzerTokenUsage(0, 0)));

    private ProjectAnalyzer BuildSut() =>
        new(_factory.Object, _prompts.Object, _dispatcher.Object,
            NullLogger<ProjectAnalyzer>.Instance);
}
