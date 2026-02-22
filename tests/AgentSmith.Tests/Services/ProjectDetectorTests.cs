using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public class ProjectDetectorTests : IDisposable
{
    private readonly ProjectDetector _sut = new(NullLogger<ProjectDetector>.Instance);
    private readonly string _tempDir;

    public ProjectDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Detect_DotNetProject_ReturnsCorrectLanguageAndRuntime()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "MyApp.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Anthropic.SDK" Version="5.9.0" />
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
                <PackageReference Include="xunit" Version="2.6.2" />
              </ItemGroup>
            </Project>
            """);

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.Language.Should().Be("C#");
        result.Runtime.Should().Contain(".NET");
        result.PackageManager.Should().Be("NuGet");
        result.BuildCommand.Should().Be("dotnet build");
        result.TestCommand.Should().Be("dotnet test");
        result.Sdks.Should().Contain("Anthropic.SDK");
    }

    [Fact]
    public void Detect_TypeScriptProject_WithPnpm_DetectsCorrectPackageManager()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """
            {
              "name": "my-app",
              "scripts": {
                "build": "vite build",
                "test": "vitest"
              },
              "dependencies": {
                "react": "^18.0.0"
              },
              "devDependencies": {
                "vitest": "^1.0.0",
                "typescript": "^5.0.0"
              }
            }
            """);
        File.WriteAllText(Path.Combine(_tempDir, "tsconfig.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "pnpm-lock.yaml"), "lockfileVersion: 9");

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.Language.Should().Be("TypeScript");
        result.PackageManager.Should().Be("pnpm");
        result.BuildCommand.Should().Be("pnpm run build");
        result.TestCommand.Should().Be("pnpm test");
        result.Sdks.Should().Contain("react");
    }

    [Fact]
    public void Detect_PythonProject_WithPoetry_DetectsCorrectPackageManager()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "pyproject.toml"), """
            [tool.poetry]
            name = "my-app"
            version = "0.1.0"

            [tool.pytest.ini_options]
            testpaths = ["tests"]
            """);

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.Language.Should().Be("Python");
        result.PackageManager.Should().Be("poetry");
        result.TestCommand.Should().Be("pytest");
    }

    [Fact]
    public void Detect_PythonProject_WithUv_DetectsCorrectPackageManager()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "pyproject.toml"), """
            [project]
            name = "my-app"
            """);
        File.WriteAllText(Path.Combine(_tempDir, "uv.lock"), "version = 1");

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.Language.Should().Be("Python");
        result.PackageManager.Should().Be("uv");
    }

    [Fact]
    public void Detect_DockerAndGitHubActions_DetectsInfrastructure()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """{"name":"test"}""");
        File.WriteAllText(Path.Combine(_tempDir, "Dockerfile"), "FROM node:20");
        Directory.CreateDirectory(Path.Combine(_tempDir, ".github", "workflows"));
        File.WriteAllText(
            Path.Combine(_tempDir, ".github", "workflows", "ci.yml"),
            "name: CI");

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.Infrastructure.Should().Contain("Docker");
        result.Infrastructure.Should().Contain("GitHub-Actions");
    }

    [Fact]
    public void Detect_NoMarkers_ReturnsUnknown()
    {
        // Arrange — empty directory

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.Language.Should().Be("Unknown");
        result.Runtime.Should().BeNull();
        result.BuildCommand.Should().BeNull();
        result.TestCommand.Should().BeNull();
    }

    [Fact]
    public void Detect_JavaScriptProject_WithYarn_DetectsCorrectly()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """
            {
              "name": "my-app",
              "scripts": { "build": "webpack", "test": "jest" }
            }
            """);
        File.WriteAllText(Path.Combine(_tempDir, "yarn.lock"), "# yarn lockfile v1");

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.Language.Should().Be("JavaScript");
        result.PackageManager.Should().Be("yarn");
        result.BuildCommand.Should().Be("yarn run build");
        result.TestCommand.Should().Be("yarn test");
    }

    [Fact]
    public void Detect_ReadsReadmeExcerpt()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """{"name":"test"}""");
        File.WriteAllText(Path.Combine(_tempDir, "README.md"),
            "# My Project\n\nThis is a test project that does interesting things.");

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.ReadmeExcerpt.Should().Contain("My Project");
    }

    [Fact]
    public void Detect_DotNetProject_CollectsKeyFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.KeyFiles.Should().Contain("App.csproj");
    }

    [Fact]
    public void Detect_KubernetesAndTerraform_DetectsInfrastructure()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), """{"name":"test"}""");
        Directory.CreateDirectory(Path.Combine(_tempDir, "k8s"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "terraform"));

        // Act
        var result = _sut.Detect(_tempDir);

        // Assert
        result.Infrastructure.Should().Contain("K8s");
        result.Infrastructure.Should().Contain("Terraform");
    }
}
