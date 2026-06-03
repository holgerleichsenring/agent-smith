using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0205: the LoadCatalog step records the per-run catalog binding. It reads the
/// <see cref="CatalogResolution"/> set by ExecutePipelineUseCase, counts skills
/// vs masters from the loaded roles, reads the concept count from the vocabulary,
/// and emits a single <see cref="CatalogLoadedEvent"/>.
/// </summary>
public sealed class LoadCatalogHandlerTests
{
    private sealed class FakeSkillLoader(IReadOnlyList<RoleSkillDefinition> roles) : ISkillLoader
    {
        public SkillConfig? LoadProjectSkills(string agentSmithDirectory) => null;
        public IReadOnlyList<RoleSkillDefinition> LoadRoleDefinitions(string skillsDirectory) => roles;
        public ConceptVocabulary LoadVocabulary(string skillsDirectory) => ConceptVocabulary.Empty;
        public IReadOnlyList<RoleSkillDefinition> GetActiveRoles(
            IReadOnlyList<RoleSkillDefinition> allRoles, SkillConfig projectSkills) => allRoles;
    }

    private static RoleSkillDefinition Role(string name, string role) =>
        new() { Name = name, Role = role };

    private static ConceptVocabulary VocabWith(int count)
    {
        var dict = new Dictionary<string, ProjectConcept>();
        for (var i = 0; i < count; i++)
            dict[$"c{i}"] = new ProjectConcept($"c{i}", "", ConceptType.Bool, null, null, []);
        return new ConceptVocabulary(dict);
    }

    [Fact]
    public async Task LoadCatalogHandler_EmitsCatalogLoaded_WithVersionSourceCounts()
    {
        var roles = new[]
        {
            Role("auth-reviewer", "investigator"),
            Role("coding-planner", "producer"),
            Role("coding-agent-master", "master"),
        };
        var publisher = new RecordingEventPublisher();
        var handler = new LoadCatalogHandler(
            new FakeSkillLoader(roles), publisher, NullLogger<LoadCatalogHandler>.Instance);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.ConceptVocabulary, VocabWith(74));
        pipeline.Set(ContextKeys.CatalogResolution,
            new CatalogResolution("/catalog", "v3.7.0", SkillsSourceMode.Default, "https://rel/v3.7.0", FromCache: true));

        var result = await handler.ExecuteAsync(new LoadCatalogContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var evt = publisher.Events.OfType<CatalogLoadedEvent>().Single();
        evt.Version.Should().Be("v3.7.0");
        evt.Source.Should().Be("Default");
        evt.SourceUrl.Should().Be("https://rel/v3.7.0");
        evt.ConceptCount.Should().Be(74);
        evt.SkillsLoaded.Should().Be(2, "two non-master roles are skills");
        evt.MastersCount.Should().Be(1, "one role is a master");
        evt.FromCache.Should().BeTrue();
    }

    [Fact]
    public async Task LoadCatalogHandler_EmitsCatalogLoaded_WithSkillMasterConceptNames()
    {
        var roles = new[]
        {
            Role("auth-reviewer", "investigator"),
            Role("coding-planner", "producer"),
            Role("coding-agent-master", "master"),
        };
        var publisher = new RecordingEventPublisher();
        var handler = new LoadCatalogHandler(
            new FakeSkillLoader(roles), publisher, NullLogger<LoadCatalogHandler>.Instance);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.ConceptVocabulary, VocabWith(3));
        pipeline.Set(ContextKeys.CatalogResolution,
            new CatalogResolution("/catalog", "v3.7.0", SkillsSourceMode.Default, "https://rel/v3.7.0", FromCache: true));

        await handler.ExecuteAsync(new LoadCatalogContext(pipeline), CancellationToken.None);

        var evt = publisher.Events.OfType<CatalogLoadedEvent>().Single();
        evt.SkillNames.Should().Equal("auth-reviewer", "coding-planner");
        evt.MasterNames.Should().Equal("coding-agent-master");
        evt.ConceptNames.Should().Equal("c0", "c1", "c2");
    }

    [Fact]
    public async Task LoadCatalogHandler_NameArrays_AreSortedAlphabetically()
    {
        var roles = new[]
        {
            Role("zeta-skill", "investigator"),
            Role("alpha-skill", "producer"),
            Role("zeta-master", "master"),
            Role("alpha-master", "master"),
        };
        var publisher = new RecordingEventPublisher();
        var handler = new LoadCatalogHandler(
            new FakeSkillLoader(roles), publisher, NullLogger<LoadCatalogHandler>.Instance);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.ConceptVocabulary, VocabUnsorted("zoo", "ant", "bee"));
        pipeline.Set(ContextKeys.CatalogResolution,
            new CatalogResolution("/catalog", "v3.7.0", SkillsSourceMode.Default, "https://rel/v3.7.0", FromCache: false));

        await handler.ExecuteAsync(new LoadCatalogContext(pipeline), CancellationToken.None);

        var evt = publisher.Events.OfType<CatalogLoadedEvent>().Single();
        evt.SkillNames.Should().Equal("alpha-skill", "zeta-skill");
        evt.MasterNames.Should().Equal("alpha-master", "zeta-master");
        evt.ConceptNames.Should().Equal("ant", "bee", "zoo");
    }

    private static ConceptVocabulary VocabUnsorted(params string[] names)
    {
        var dict = new Dictionary<string, ProjectConcept>();
        foreach (var name in names)
            dict[name] = new ProjectConcept(name, "", ConceptType.Bool, null, null, []);
        return new ConceptVocabulary(dict);
    }

    [Fact]
    public async Task LoadCatalogHandler_NoCatalogResolution_SkipsWithoutEmitting()
    {
        var publisher = new RecordingEventPublisher();
        var handler = new LoadCatalogHandler(
            new FakeSkillLoader([]), publisher, NullLogger<LoadCatalogHandler>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");

        var result = await handler.ExecuteAsync(new LoadCatalogContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        publisher.Events.Should().BeEmpty("no binding to report when the resolver was bypassed");
    }
}
