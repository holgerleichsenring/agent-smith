using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class ContextGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public ContextGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "agentsmith-ctxgen-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void StripCodeFences_PlainYaml_ReturnsUnchanged()
    {
        // Arrange
        var yaml = "meta:\n  project: test";

        // Act
        var result = ContextGenerator.StripCodeFences(yaml);

        // Assert
        result.Should().Be(yaml);
    }

    [Fact]
    public void StripCodeFences_YamlFences_StripsCodeBlock()
    {
        // Arrange
        var yaml = "```yaml\nmeta:\n  project: test\n```";

        // Act
        var result = ContextGenerator.StripCodeFences(yaml);

        // Assert
        result.Should().Be("meta:\n  project: test");
    }

    [Fact]
    public void StripCodeFences_GenericFences_StripsCodeBlock()
    {
        // Arrange
        var yaml = "```\nmeta:\n  project: test\n```";

        // Act
        var result = ContextGenerator.StripCodeFences(yaml);

        // Assert
        result.Should().Be("meta:\n  project: test");
    }

    [Fact]
    public void GenerateTree_EmptyDir_ReturnsEmpty()
    {
        // Act
        var result = ContextGenerator.GenerateTree(_tempDir, 3);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateTree_WithFiles_ReturnsStructure()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "README.md"), "# Test");
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));
        File.WriteAllText(Path.Combine(_tempDir, "src", "main.cs"), "class Main {}");

        // Act
        var result = ContextGenerator.GenerateTree(_tempDir, 3);

        // Assert
        result.Should().Contain("README.md");
        result.Should().Contain("src/");
        result.Should().Contain("main.cs");
    }

    [Fact]
    public void GenerateTree_ExcludesGitAndNodeModules()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "node_modules"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));

        // Act
        var result = ContextGenerator.GenerateTree(_tempDir, 3);

        // Assert
        result.Should().Contain("src/");
        result.Should().NotContain(".git");
        result.Should().NotContain("node_modules");
    }

    [Fact]
    public void BuildUserPrompt_IncludesDetectedStack()
    {
        // Arrange
        var project = CreateDetectedProject("C#", ".NET 8", "NuGet");

        // Act
        var result = ContextGenerator.BuildUserPrompt(project, "", "");

        // Assert
        result.Should().Contain("C#");
        result.Should().Contain(".NET 8");
        result.Should().Contain("NuGet");
    }

    [Fact]
    public void BuildUserPrompt_IncludesReadmeExcerpt()
    {
        // Arrange
        var project = CreateDetectedProject("Python", "Python", "pip",
            readmeExcerpt: "This is a Django web application.");

        // Act
        var result = ContextGenerator.BuildUserPrompt(project, "", "");

        // Assert
        result.Should().Contain("README (excerpt)");
        result.Should().Contain("Django web application");
    }

    [Fact]
    public void BuildUserPrompt_OmitsReadmeSectionWhenNull()
    {
        // Arrange
        var project = CreateDetectedProject("TypeScript", "Node.js", "npm");

        // Act
        var result = ContextGenerator.BuildUserPrompt(project, "", "");

        // Assert
        result.Should().NotContain("README (excerpt)");
    }

    [Fact]
    public void ReadKeyFiles_TruncatesLargeFiles()
    {
        // Arrange
        var fileName = "large.csproj";
        File.WriteAllText(Path.Combine(_tempDir, fileName), new string('x', 5000));

        // Act
        var result = ContextGenerator.ReadKeyFiles([fileName], _tempDir);

        // Assert
        result.Should().Contain("(truncated)");
        result.Should().Contain(fileName);
    }

    [Fact]
    public void ReadKeyFiles_SkipsMissingFiles()
    {
        // Act
        var result = ContextGenerator.ReadKeyFiles(["nonexistent.txt"], _tempDir);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadKeyFiles_ReadsExistingFile()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, "package.json"),
            """{"name": "my-app"}""");

        // Act
        var result = ContextGenerator.ReadKeyFiles(["package.json"], _tempDir);

        // Assert
        result.Should().Contain("package.json");
        result.Should().Contain("my-app");
    }

    private static DetectedProject CreateDetectedProject(
        string language, string runtime, string packageManager,
        string? readmeExcerpt = null) =>
        new(
            Language: language,
            Runtime: runtime,
            PackageManager: packageManager,
            BuildCommand: "build",
            TestCommand: "test",
            Frameworks: [],
            Infrastructure: [],
            KeyFiles: [],
            Sdks: [],
            ReadmeExcerpt: readmeExcerpt);
}
