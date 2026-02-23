using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public class BootstrapProjectHandlerTests : IDisposable
{
    private readonly Mock<IProjectDetector> _detector = new();
    private readonly Mock<IRepoSnapshotCollector> _snapshotCollector = new();
    private readonly Mock<IContextGenerator> _generator = new();
    private readonly Mock<IContextValidator> _validator = new();
    private readonly Mock<ICodeMapGenerator> _codeMapGenerator = new();
    private readonly Mock<ICodingPrinciplesGenerator> _codingPrinciplesGenerator = new();
    private readonly BootstrapProjectHandler _sut;
    private readonly string _tempDir;

    public BootstrapProjectHandlerTests()
    {
        _sut = new BootstrapProjectHandler(
            _detector.Object,
            _snapshotCollector.Object,
            _generator.Object,
            _validator.Object,
            _codeMapGenerator.Object,
            _codingPrinciplesGenerator.Object,
            NullLogger<BootstrapProjectHandler>.Instance);

        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-bootstrap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, ".agentsmith"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Execute_ExistingContextYaml_SkipsGeneration()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Existing");
        _generator.Verify(g => g.GenerateAsync(It.IsAny<DetectedProject>(), It.IsAny<string>(), It.IsAny<RepoSnapshot?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_NewRepo_GeneratesAndValidates()
    {
        // Arrange
        var detected = CreateDetectedProject("TypeScript");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var yaml = "meta:\n  project: test\nstack:\n  runtime: Node.js\n  lang: TypeScript\narch:\n  style: [Layered]\n  layers: [src]\nquality:\n  lang: english-only\nstate:\n  done: {}\n  active: {}";
        _generator.Setup(g => g.GenerateAsync(detected, _tempDir, It.IsAny<RepoSnapshot?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(yaml);
        _validator.Setup(v => v.Validate(yaml))
            .Returns(ContextValidationResult.Success());

        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, ".agentsmith", "context.yaml")).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_InvalidGeneration_RetriesOnce()
    {
        // Arrange
        var detected = CreateDetectedProject("Python");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var badYaml = "invalid";
        var goodYaml = "meta:\n  project: test\nstack:\n  runtime: Python\n  lang: Python\narch:\n  style: [Layered]\n  layers: [src]\nquality:\n  lang: english-only\nstate:\n  done: {}\n  active: {}";
        var errors = new List<string> { "Missing section: meta" };

        _generator.Setup(g => g.GenerateAsync(detected, _tempDir, It.IsAny<RepoSnapshot?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(badYaml);
        _generator.Setup(g => g.RetryWithErrorsAsync(detected, _tempDir, badYaml, errors, It.IsAny<CancellationToken>()))
            .ReturnsAsync(goodYaml);

        _validator.Setup(v => v.Validate(badYaml))
            .Returns(ContextValidationResult.Failure(errors));
        _validator.Setup(v => v.Validate(goodYaml))
            .Returns(ContextValidationResult.Success());

        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _generator.Verify(
            g => g.RetryWithErrorsAsync(It.IsAny<DetectedProject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_StoresDetectedProjectInPipeline()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var context = CreateContext();

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        context.Pipeline.TryGet<DetectedProject>(ContextKeys.DetectedProject, out var stored).Should().BeTrue();
        stored.Should().Be(detected);
    }

    [Fact]
    public async Task Execute_NoCodeMap_GeneratesCodeMap()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        _codeMapGenerator
            .Setup(g => g.GenerateAsync(detected, _tempDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync("modules:\n  - name: Core");

        var context = CreateContext();

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        File.Exists(Path.Combine(_tempDir, ".agentsmith", "code-map.yaml")).Should().BeTrue();
        _codeMapGenerator.Verify(
            g => g.GenerateAsync(It.IsAny<DetectedProject>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ExistingCodeMap_SkipsGeneration()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "code-map.yaml"), "modules: []");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var context = CreateContext();

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        _codeMapGenerator.Verify(
            g => g.GenerateAsync(It.IsAny<DetectedProject>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_CodeMapGenerationFails_ContinuesSuccessfully()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        _codeMapGenerator
            .Setup(g => g.GenerateAsync(detected, _tempDir, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM API failure"));

        var context = CreateContext();

        // Act
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, ".agentsmith", "code-map.yaml")).Should().BeFalse();
    }

    private BootstrapProjectContext CreateContext()
    {
        var repo = new Repository(_tempDir, new BranchName("feature/test"), "https://github.com/test/test");
        return new BootstrapProjectContext(repo, new PipelineContext());
    }

    private static DetectedProject CreateDetectedProject(string language) =>
        new(
            Language: language,
            Runtime: language == "C#" ? ".NET 8" : language == "TypeScript" ? "Node.js" : "Python",
            PackageManager: language == "C#" ? "NuGet" : "npm",
            BuildCommand: "dotnet build",
            TestCommand: "dotnet test",
            Frameworks: [],
            Infrastructure: [],
            KeyFiles: [],
            Sdks: []);
}
