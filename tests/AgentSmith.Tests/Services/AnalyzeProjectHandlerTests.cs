using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class AnalyzeProjectHandlerTests : IDisposable
{
    private readonly string _repoPath;
    private readonly Mock<IProjectAnalyzer> _analyzer = new();
    private readonly Mock<IProjectMetaResolver> _metaResolver = new();

    public AnalyzeProjectHandlerTests()
    {
        _repoPath = Path.Combine(Path.GetTempPath(), $"analyze-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoPath);
        var metaDir = Path.Combine(_repoPath, ".agentsmith");
        Directory.CreateDirectory(metaDir);
        _metaResolver.Setup(r => r.Resolve(_repoPath)).Returns(metaDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoPath)) Directory.Delete(_repoPath, recursive: true);
    }

    [Fact]
    public async Task ExecuteAsync_AuthPortShape_TestProjectSurfacesInProjectMap()
    {
        // Regression: the AuthPort case where 117 test files in *.Tests.csproj were
        // invisible to the Tester gate, leading to hallucinated coverage vetos.
        // This test asserts the wire-up: a project with a discoverable test project
        // produces a ProjectMap whose TestProjects collection is non-empty AND lands
        // in PipelineContext, so SkillRoundHandlerBase can render it.
        File.WriteAllText(Path.Combine(_repoPath, "RHS.AuthPort.csproj"), "<Project></Project>");

        var authPortMap = new ProjectMap(
            "C#", [".NET 8"],
            [new Module("src/RHS.AuthPort", ModuleRole.Production, [])],
            [new TestProject("RHS.AuthPort.Tests.Integration", "xUnit", 117, "tests/Sample.cs")],
            ["src/RHS.AuthPort/Program.cs"],
            new Conventions(null, null, null),
            new CiConfig(false, null, null, null));

        _analyzer.Setup(a => a.AnalyzeAsync(_repoPath, It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authPortMap);

        var (sut, ctx) = BuildSut();
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var stored = ctx.Pipeline.Get<ProjectMap>(ContextKeys.ProjectMap);
        stored.TestProjects.Should().HaveCount(1);
        stored.TestProjects[0].FileCount.Should().Be(117);
        stored.TestProjects[0].Framework.Should().Be("xUnit");

        // Tester gate ExistingTests block renders from this:
        var rendered = ProjectMapPromptRenderer.RenderExistingTests(stored);
        rendered.Should().Contain("RHS.AuthPort.Tests.Integration");
        rendered.Should().Contain("117 test file(s)");
    }

    [Fact]
    public async Task ExecuteAsync_TransitionalDualPopulation_FillsCodeAnalysisAndCodeMap()
    {
        File.WriteAllText(Path.Combine(_repoPath, "App.csproj"), "<Project></Project>");
        var map = new ProjectMap("C#", [".NET 8"],
            [new Module("src/App", ModuleRole.Production, [])],
            [], [], new Conventions(null, null, null),
            new CiConfig(false, null, null, null));

        _analyzer.Setup(a => a.AnalyzeAsync(_repoPath, It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(map);

        var (sut, ctx) = BuildSut();
        await sut.ExecuteAsync(ctx, CancellationToken.None);

        ctx.Pipeline.TryGet<CodeAnalysis>(ContextKeys.CodeAnalysis, out var legacy)
            .Should().BeTrue("legacy ContextKeys.CodeAnalysis must remain populated until p0110c");
        legacy!.Language.Should().Be("C#");

        ctx.Pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMapText)
            .Should().BeTrue();
        codeMapText.Should().Contain("primary_language: C#");
    }

    [Fact]
    public async Task ExecuteAsync_CacheHit_SkipsAnalyzer()
    {
        // Pre-populate cache: write a manifest, run once to fill the cache, run again to
        // confirm the second run uses the cached file (analyzer NOT invoked).
        File.WriteAllText(Path.Combine(_repoPath, "App.csproj"), "<Project></Project>");
        var map = new ProjectMap("C#", [".NET 8"], [], [], [], new Conventions(null, null, null),
            new CiConfig(false, null, null, null));
        _analyzer.Setup(a => a.AnalyzeAsync(_repoPath, It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(map);

        var (sut, ctx1) = BuildSut();
        await sut.ExecuteAsync(ctx1, CancellationToken.None);

        var (_, ctx2) = BuildSut();
        await sut.ExecuteAsync(ctx2, CancellationToken.None);

        _analyzer.Verify(a => a.AnalyzeAsync(_repoPath, It.IsAny<AgentConfig>(), It.IsAny<CancellationToken>()),
            Times.Once, "second run should hit the cached project-map.json");
    }

    private (AnalyzeProjectHandler Sut, AnalyzeCodeContext Context) BuildSut()
    {
        var pipeline = new PipelineContext();
        // Resolved pipeline is required for context.Pipeline.Resolved().Agent
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("fix-bug", new AgentConfig { Type = "claude", Model = "x" },
                "skills/coding", null));

        var ctx = new AnalyzeCodeContext(new Repository(_repoPath, new BranchName("main"), "https://example.com/repo.git"), pipeline);
        var sut = new AnalyzeProjectHandler(_analyzer.Object, _metaResolver.Object,
            NullLogger<AnalyzeProjectHandler>.Instance);
        return (sut, ctx);
    }
}
