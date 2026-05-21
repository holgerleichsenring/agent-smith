using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Activation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

public sealed class PublishProjectLanguageHandlerTests
{
    private static readonly ConceptVocabulary Vocab = new(new Dictionary<string, ProjectConcept>
    {
        ["project_language"] = new("project_language", "test", ConceptType.String, null, null, []),
    });

    private readonly Func<PipelineContext, IRunStateConcepts> _conceptsFactory =
        ctx => new PipelineContextRunStateConcepts(ctx, Vocab);

    private PublishProjectLanguageHandler Handler() => new(
        _conceptsFactory, NullLogger<PublishProjectLanguageHandler>.Instance);

    [Fact]
    public async Task Publishes_LoweredTrimmedPrimaryLanguage_Verbatim()
    {
        var pipeline = PipelineWith(primaryLanguage: "  CSharp  ");

        var result = await Handler().ExecuteAsync(
            new PublishProjectLanguageContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _conceptsFactory(pipeline).GetString("project_language").Should().Be("csharp");
    }

    [Fact]
    public async Task Publishes_ArbitraryLanguage_VerbatimWithoutMapping()
    {
        var pipeline = PipelineWith(primaryLanguage: "lua");

        await Handler().ExecuteAsync(
            new PublishProjectLanguageContext(pipeline), CancellationToken.None);

        _conceptsFactory(pipeline).GetString("project_language").Should().Be("lua");
    }

    [Fact]
    public async Task Fails_WhenProjectMapMissing()
    {
        var pipeline = new PipelineContext();

        var result = await Handler().ExecuteAsync(
            new PublishProjectLanguageContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("ContextKeys.ProjectMap is missing");
    }

    [Fact]
    public async Task Fails_WhenPrimaryLanguageNullOrEmpty()
    {
        var pipeline = PipelineWith(primaryLanguage: "");

        var result = await Handler().ExecuteAsync(
            new PublishProjectLanguageContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("PrimaryLanguage is null or empty");
        result.Message.Should().Contain("'generic'");
    }

    [Fact]
    public async Task Publishes_Generic_WhenAnalyzerExplicitlyEmitsGeneric()
    {
        // Generic is a deliberate analyzer choice now, not a silent fallback.
        var pipeline = PipelineWith(primaryLanguage: "generic");

        await Handler().ExecuteAsync(
            new PublishProjectLanguageContext(pipeline), CancellationToken.None);

        _conceptsFactory(pipeline).GetString("project_language").Should().Be("generic");
    }

    private static PipelineContext PipelineWith(string primaryLanguage)
    {
        var pipeline = new PipelineContext();
        var map = new ProjectMap(
            PrimaryLanguage: primaryLanguage,
            Frameworks: Array.Empty<string>(),
            Modules: Array.Empty<Module>(),
            TestProjects: Array.Empty<TestProject>(),
            EntryPoints: Array.Empty<string>(),
            Conventions: new Conventions("", "", ""),
            Ci: new CiConfig(false, null, null, null));
        pipeline.Set(ContextKeys.ProjectMap, map);
        return pipeline;
    }
}
