using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
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
    private readonly Mock<ILlmClientFactory> _llmClientFactory = new();
    private readonly Mock<ILlmClient> _llmClient = new();
    private readonly Mock<IContextGenerator> _generator = new();
    private readonly Mock<IContextValidator> _validator = new();
    private readonly Mock<ICodeMapGenerator> _codeMapGenerator = new();
    private readonly Mock<ICodingPrinciplesGenerator> _codingPrinciplesGenerator = new();
    private readonly Mock<ISkillLoader> _skillLoader = new();
    private readonly BootstrapProjectHandler _sut;
    private readonly string _tempDir;

    public BootstrapProjectHandlerTests()
    {
        _llmClientFactory.Setup(f => f.Create(It.IsAny<AgentConfig>())).Returns(_llmClient.Object);

        var metaFileBootstrapper = new MetaFileBootstrapper(
            _codeMapGenerator.Object,
            _codingPrinciplesGenerator.Object,
            _skillLoader.Object,
            NullLogger<MetaFileBootstrapper>.Instance);

        _sut = new BootstrapProjectHandler(
            _detector.Object,
            _snapshotCollector.Object,
            _llmClientFactory.Object,
            _generator.Object,
            _validator.Object,
            metaFileBootstrapper,
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
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var context = CreateContext();

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Existing");
        _generator.Verify(g => g.GenerateAsync(
            It.IsAny<DetectedProject>(), It.IsAny<string>(),
            It.IsAny<RepoSnapshot?>(), It.IsAny<ILlmClient>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_NewRepo_GeneratesAndValidates()
    {
        var detected = CreateDetectedProject("TypeScript");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var yaml = "meta:\n  project: test\nstack:\n  runtime: Node.js\n  lang: TypeScript\narch:\n  style: [Layered]\n  layers: [src]\nquality:\n  lang: english-only\nstate:\n  done: {}\n  active: {}";
        _generator.Setup(g => g.GenerateAsync(
                detected, _tempDir, It.IsAny<RepoSnapshot?>(),
                It.IsAny<ILlmClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(yaml);
        _validator.Setup(v => v.Validate(yaml))
            .Returns(ContextValidationResult.Success());

        var context = CreateContext();

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, ".agentsmith", "context.yaml")).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_InvalidGeneration_RetriesOnce()
    {
        var detected = CreateDetectedProject("Python");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var badYaml = "invalid";
        var goodYaml = "meta:\n  project: test\nstack:\n  runtime: Python\n  lang: Python\narch:\n  style: [Layered]\n  layers: [src]\nquality:\n  lang: english-only\nstate:\n  done: {}\n  active: {}";
        var errors = new List<string> { "Missing section: meta" };

        _generator.Setup(g => g.GenerateAsync(
                detected, _tempDir, It.IsAny<RepoSnapshot?>(),
                It.IsAny<ILlmClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(badYaml);
        _generator.Setup(g => g.RetryWithErrorsAsync(
                detected, _tempDir, badYaml, errors,
                It.IsAny<ILlmClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(goodYaml);

        _validator.Setup(v => v.Validate(badYaml))
            .Returns(ContextValidationResult.Failure(errors));
        _validator.Setup(v => v.Validate(goodYaml))
            .Returns(ContextValidationResult.Success());

        var context = CreateContext();

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _generator.Verify(
            g => g.RetryWithErrorsAsync(
                It.IsAny<DetectedProject>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<ILlmClient>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_StoresDetectedProjectInPipeline()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var context = CreateContext();

        await _sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<DetectedProject>(ContextKeys.DetectedProject, out var stored).Should().BeTrue();
        stored.Should().Be(detected);
    }

    [Fact]
    public async Task Execute_NoCodeMap_GeneratesCodeMap()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        _codeMapGenerator
            .Setup(g => g.GenerateAsync(
                detected, _tempDir, It.IsAny<RepoSnapshot>(),
                It.IsAny<ILlmClient>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("modules:\n  - name: Core");

        var context = CreateContext();

        await _sut.ExecuteAsync(context, CancellationToken.None);

        File.Exists(Path.Combine(_tempDir, ".agentsmith", "code-map.yaml")).Should().BeTrue();
        _codeMapGenerator.Verify(
            g => g.GenerateAsync(
                It.IsAny<DetectedProject>(), It.IsAny<string>(),
                It.IsAny<RepoSnapshot>(), It.IsAny<ILlmClient>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ExistingCodeMap_SkipsGeneration()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "code-map.yaml"), "modules: []");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var context = CreateContext();

        await _sut.ExecuteAsync(context, CancellationToken.None);

        _codeMapGenerator.Verify(
            g => g.GenerateAsync(
                It.IsAny<DetectedProject>(), It.IsAny<string>(),
                It.IsAny<RepoSnapshot>(), It.IsAny<ILlmClient>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_CodeMapGenerationFails_ContinuesSuccessfully()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        _codeMapGenerator
            .Setup(g => g.GenerateAsync(
                detected, _tempDir, It.IsAny<RepoSnapshot>(),
                It.IsAny<ILlmClient>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM API failure"));

        var context = CreateContext();

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, ".agentsmith", "code-map.yaml")).Should().BeFalse();
    }

    [Fact]
    public async Task Execute_CreatesLlmClientFromAgentConfig()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var context = CreateContext();

        await _sut.ExecuteAsync(context, CancellationToken.None);

        _llmClientFactory.Verify(f => f.Create(context.Agent), Times.Once);
    }

    private BootstrapProjectContext CreateContext()
    {
        var repo = new Repository(_tempDir, new BranchName("feature/test"), "https://github.com/test/test");
        return new BootstrapProjectContext(repo, new AgentConfig { Type = "claude" }, "config/skills/coding", new PipelineContext());
    }

    [Fact]
    public async Task Execute_SkillLoaderReturnsRoles_StoresInPipeline()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var roles = new List<RoleSkillDefinition>
        {
            new() { Name = "architect", DisplayName = "Architect", Rules = "Design rules" },
            new() { Name = "tester", DisplayName = "Tester", Rules = "Test rules" }
        };
        _skillLoader.Setup(s => s.LoadRoleDefinitions(It.IsAny<string>())).Returns(roles);
        _skillLoader.Setup(s => s.LoadProjectSkills(It.IsAny<string>())).Returns((SkillConfig?)null);

        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, out var stored).Should().BeTrue();
        stored.Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_SkillYamlExists_FiltersRolesViaGetActiveRoles()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        var allRoles = new List<RoleSkillDefinition>
        {
            new() { Name = "architect", DisplayName = "Architect", Rules = "Design rules" },
            new() { Name = "tester", DisplayName = "Tester", Rules = "Test rules" }
        };
        var filteredRoles = new List<RoleSkillDefinition>
        {
            new() { Name = "architect", DisplayName = "Architect", Rules = "Design rules" }
        };
        var skillConfig = new SkillConfig
        {
            Roles = new Dictionary<string, RoleProjectConfig>
            {
                ["architect"] = new() { Enabled = true }
            }
        };

        _skillLoader.Setup(s => s.LoadRoleDefinitions(It.IsAny<string>())).Returns(allRoles);
        _skillLoader.Setup(s => s.LoadProjectSkills(It.IsAny<string>())).Returns(skillConfig);
        _skillLoader.Setup(s => s.GetActiveRoles(allRoles, skillConfig)).Returns(filteredRoles);

        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, out var stored).Should().BeTrue();
        stored.Should().HaveCount(1);
        stored![0].Name.Should().Be("architect");

        context.Pipeline.TryGet<SkillConfig>(
            ContextKeys.ProjectSkills, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_NoRoleDefinitions_DoesNotStoreRoles()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        _skillLoader.Setup(s => s.LoadRoleDefinitions(It.IsAny<string>()))
            .Returns(new List<RoleSkillDefinition>());

        var context = CreateContext();
        await _sut.ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Execute_SkillLoaderThrows_ContinuesSuccessfully()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".agentsmith", "context.yaml"), "meta: {}");

        var detected = CreateDetectedProject("C#");
        _detector.Setup(d => d.Detect(_tempDir)).Returns(detected);

        _skillLoader.Setup(s => s.LoadRoleDefinitions(It.IsAny<string>()))
            .Throws(new Exception("Skills directory missing"));

        var context = CreateContext();
        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, out _).Should().BeFalse();
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
