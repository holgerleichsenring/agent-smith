using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Detection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Integration;

public class ProjectDetectorIntegrationTests
{
    private static readonly ILanguageDetector[] Detectors =
    [
        new DotNetLanguageDetector(NullLogger<DotNetLanguageDetector>.Instance),
        new TypeScriptLanguageDetector(NullLogger<TypeScriptLanguageDetector>.Instance),
        new PythonLanguageDetector()
    ];

    private readonly ProjectDetector _sut = new(Detectors, NullLogger<ProjectDetector>.Instance);

    [Fact]
    public void Detect_AgentSmithRepo_ReturnsCorrectProjectInfo()
    {
        // Arrange — use the actual repo root (two levels up from test bin)
        var repoRoot = FindRepoRoot();

        // Act
        var result = _sut.Detect(repoRoot);

        // Assert
        result.Language.Should().Be("C#");
        result.Runtime.Should().Contain(".NET");
        result.PackageManager.Should().Be("NuGet");
        result.BuildCommand.Should().Be("dotnet build");
        result.TestCommand.Should().Be("dotnet test");
        result.Infrastructure.Should().Contain("Docker");
        result.Sdks.Should().Contain(s => s.Contains("Anthropic"));
        result.Sdks.Should().Contain(s => s.Contains("Octokit"));
        result.Sdks.Should().Contain(s => s.Contains("LibGit2Sharp"));
        result.KeyFiles.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Detect_AgentSmithRepo_DetectsGitHubActions()
    {
        // Arrange
        var repoRoot = FindRepoRoot();

        // Act
        var result = _sut.Detect(repoRoot);

        // Assert — agent-smith has .github/workflows
        if (Directory.Exists(Path.Combine(repoRoot, ".github", "workflows")))
            result.Infrastructure.Should().Contain("GitHub-Actions");
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "AgentSmith.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "Could not find AgentSmith.sln. Run tests from within the repo.");
    }
}
