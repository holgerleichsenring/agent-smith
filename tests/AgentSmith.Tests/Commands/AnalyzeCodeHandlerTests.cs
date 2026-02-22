using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public class AnalyzeCodeHandlerTests : IDisposable
{
    private readonly string _repoPath;
    private readonly AnalyzeCodeHandler _sut;

    public AnalyzeCodeHandlerTests()
    {
        _repoPath = Path.Combine(Path.GetTempPath(), "agentsmith-analyze-" + Guid.NewGuid());
        Directory.CreateDirectory(_repoPath);
        _sut = new AnalyzeCodeHandler(NullLogger<AnalyzeCodeHandler>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoPath))
            Directory.Delete(_repoPath, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_DotnetProject_DetectsLanguageAndFramework()
    {
        File.WriteAllText(Path.Combine(_repoPath, "Test.csproj"),
            """<Project><ItemGroup><PackageReference Include="Moq" Version="4.0" /></ItemGroup></Project>""");
        Directory.CreateDirectory(Path.Combine(_repoPath, "src"));
        File.WriteAllText(Path.Combine(_repoPath, "src", "Program.cs"), "");

        var repo = new Repository(_repoPath, new BranchName("main"), "https://test.git");
        var pipeline = new PipelineContext();
        var context = new AnalyzeCodeContext(repo, pipeline);

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();

        var analysis = pipeline.Get<CodeAnalysis>(ContextKeys.CodeAnalysis);
        analysis.Language.Should().Be("C#");
        analysis.Framework.Should().Be("dotnet");
        analysis.FileStructure.Should().Contain(f => f.Contains("Program.cs"));
        analysis.Dependencies.Should().Contain(d => d.Contains("Moq"));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyRepo_ReturnsEmptyAnalysis()
    {
        var repo = new Repository(_repoPath, new BranchName("main"), "https://test.git");
        var pipeline = new PipelineContext();
        var context = new AnalyzeCodeContext(repo, pipeline);

        var result = await _sut.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();

        var analysis = pipeline.Get<CodeAnalysis>(ContextKeys.CodeAnalysis);
        analysis.FileStructure.Should().BeEmpty();
        analysis.Language.Should().BeNull();
        analysis.Framework.Should().BeNull();
    }
}
