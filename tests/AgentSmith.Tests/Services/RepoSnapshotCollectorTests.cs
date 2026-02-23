using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

public class RepoSnapshotCollectorTests : IDisposable
{
    private readonly RepoSnapshotCollector _sut = new(NullLogger<RepoSnapshotCollector>.Instance);
    private readonly string _tempDir;

    public RepoSnapshotCollectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "agentsmith-snapshot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Collect_EditorConfig_ReadsContent()
    {
        // Arrange
        var content = "root = true\n\n[*]\nindent_style = space\nindent_size = 4";
        File.WriteAllText(Path.Combine(_tempDir, ".editorconfig"), content);

        // Act
        var snapshot = _sut.Collect(_tempDir, CreateProject("C#"));

        // Assert
        snapshot.ConfigFileContents.Should().ContainSingle();
        snapshot.ConfigFileContents[0].Should().Contain(".editorconfig");
        snapshot.ConfigFileContents[0].Should().Contain("indent_style = space");
    }

    [Fact]
    public void Collect_EslintConfig_ReadsContent()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, ".eslintrc.json"),
            """{"extends": "eslint:recommended"}""");

        // Act
        var snapshot = _sut.Collect(_tempDir, CreateProject("TypeScript"));

        // Assert
        snapshot.ConfigFileContents.Should().ContainSingle();
        snapshot.ConfigFileContents[0].Should().Contain(".eslintrc.json");
        snapshot.ConfigFileContents[0].Should().Contain("eslint:recommended");
    }

    [Fact]
    public void Collect_MultipleConfigFiles_ReadsAll()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, ".editorconfig"), "root = true");
        File.WriteAllText(Path.Combine(_tempDir, ".prettierrc"), """{"semi": true}""");

        // Act
        var snapshot = _sut.Collect(_tempDir, CreateProject("TypeScript"));

        // Assert
        snapshot.ConfigFileContents.Should().HaveCount(2);
    }

    [Fact]
    public void Collect_CodeSamples_CollectsLargestFiles()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);

        File.WriteAllText(Path.Combine(srcDir, "Small.cs"), "class Small {}");
        File.WriteAllText(Path.Combine(srcDir, "Large.cs"),
            string.Join('\n', Enumerable.Range(1, 50).Select(i => $"// Line {i}")));

        // Act
        var snapshot = _sut.Collect(_tempDir, CreateProject("C#"));

        // Assert
        snapshot.CodeSamples.Should().HaveCount(2);
        snapshot.CodeSamples[0].Should().Contain("Large.cs");
    }

    [Fact]
    public void Collect_CodeSamples_CapsAtMaxChars()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);

        for (var i = 0; i < 20; i++)
        {
            File.WriteAllText(Path.Combine(srcDir, $"File{i}.cs"),
                string.Join('\n', Enumerable.Range(1, 80).Select(l => $"// Long line {l} with padding {new string('x', 50)}")));
        }

        // Act
        var snapshot = _sut.Collect(_tempDir, CreateProject("C#"));

        // Assert
        var totalChars = snapshot.CodeSamples.Sum(s => s.Length);
        totalChars.Should().BeLessThan(15_001); // generous bound including headers
    }

    [Fact]
    public void Collect_EmptyRepo_ReturnsEmptySnapshot()
    {
        // Act
        var snapshot = _sut.Collect(_tempDir, CreateProject("C#"));

        // Assert
        snapshot.ConfigFileContents.Should().BeEmpty();
        snapshot.CodeSamples.Should().BeEmpty();
    }

    [Fact]
    public void Collect_ExcludesNodeModules()
    {
        // Arrange
        var excluded = Path.Combine(_tempDir, "node_modules", "pkg");
        Directory.CreateDirectory(excluded);
        File.WriteAllText(Path.Combine(excluded, "index.js"), "module.exports = {}");

        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "app.js"), "const app = require('express')();");

        // Act
        var snapshot = _sut.Collect(_tempDir, CreateProject("JavaScript"));

        // Assert
        snapshot.CodeSamples.Should().ContainSingle();
        snapshot.CodeSamples[0].Should().Contain("app.js");
    }

    [Fact]
    public void CollectConfigFiles_Static_ReturnsConfigContents()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, ".editorconfig"), "root = true");

        // Act
        var result = RepoSnapshotCollector.CollectConfigFiles(_tempDir);

        // Assert
        result.Should().ContainSingle();
        result[0].Should().Contain("root = true");
    }

    [Fact]
    public void CollectCodeSamples_Static_LanguageFiltering()
    {
        // Arrange
        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "app.py"), "print('hello')");
        File.WriteAllText(Path.Combine(srcDir, "utils.cs"), "class Utils {}");

        // Act
        var result = RepoSnapshotCollector.CollectCodeSamples(_tempDir, CreateProject("Python"));

        // Assert
        result.Should().ContainSingle();
        result[0].Should().Contain("app.py");
    }

    private static DetectedProject CreateProject(string language) =>
        new(
            Language: language,
            Runtime: language == "C#" ? ".NET 8" : "Node.js",
            PackageManager: "npm",
            BuildCommand: "build",
            TestCommand: "test",
            Frameworks: [],
            Infrastructure: [],
            KeyFiles: [],
            Sdks: []);
}
