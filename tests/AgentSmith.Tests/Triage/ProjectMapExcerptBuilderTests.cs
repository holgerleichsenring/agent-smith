using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

public sealed class ProjectMapExcerptBuilderTests
{
    private readonly ProjectMapExcerptBuilder _sut = new();

    [Fact]
    public void Build_DotnetWithDb_ProducesPersistenceConcept()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectMap, MapWith("csharp", new[] { "EntityFramework" }, Modules("src/Persistence", "src/Web")));
        pipeline.Set(ContextKeys.ConceptVocabulary, VocabularyWith("persistence", "web"));

        var excerpt = _sut.Build(pipeline);

        excerpt.Concepts.Should().Contain("persistence");
    }

    [Fact]
    public void Build_FrontendOnly_OmitsPersistence()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectMap, MapWith("typescript", new[] { "react" }, Modules("src/components", "src/pages")));
        pipeline.Set(ContextKeys.ConceptVocabulary, VocabularyWith("persistence", "react"));

        var excerpt = _sut.Build(pipeline);

        excerpt.Concepts.Should().Contain("react");
        excerpt.Concepts.Should().NotContain("persistence");
    }

    [Fact]
    public void Build_ProjectMapMissing_ReturnsExcerptWithUnknownType()
    {
        var pipeline = new PipelineContext();

        var excerpt = _sut.Build(pipeline);

        excerpt.Type.Should().Be("unknown");
        excerpt.Stack.Should().BeEmpty();
        excerpt.Concepts.Should().BeEmpty();
    }

    [Fact]
    public void Build_TestProjectsPresentAndCiTestCommandSet_TestCapabilityRunnable()
    {
        var pipeline = new PipelineContext();
        var testProjects = new[] { new TestProject("tests/UnitTests", "xunit", 42, "tests/UnitTests/SampleTest.cs") };
        var ci = new CiConfig(true, "dotnet build", "dotnet test", "github-actions");
        var map = new ProjectMap("csharp", new[] { "net8" }, Array.Empty<Module>(), testProjects, Array.Empty<string>(),
            new Conventions(null, null, null), ci);
        pipeline.Set(ContextKeys.ProjectMap, map);

        var excerpt = _sut.Build(pipeline);

        excerpt.TestCapability.HasTestSetup.Should().BeTrue();
        excerpt.TestCapability.RunnableInPipeline.Should().BeTrue();
        excerpt.TestCapability.TestCommand.Should().Be("dotnet test");
        excerpt.CiCapability.HasPipeline.Should().BeTrue();
    }

    private static ProjectMap MapWith(string lang, string[] frameworks, IReadOnlyList<Module> modules) =>
        new(lang, frameworks, modules, Array.Empty<TestProject>(), Array.Empty<string>(),
            new Conventions(null, null, null), new CiConfig(false, null, null, null));

    private static IReadOnlyList<Module> Modules(params string[] paths) =>
        paths.Select(p => new Module(p, ModuleRole.Production, Array.Empty<string>())).ToList();

    private static ConceptVocabulary VocabularyWith(params string[] keys)
    {
        var dict = keys.ToDictionary(k => k, k => new ProjectConcept(k, $"Concept {k}", "project_concepts"));
        return new ConceptVocabulary(dict);
    }
}
