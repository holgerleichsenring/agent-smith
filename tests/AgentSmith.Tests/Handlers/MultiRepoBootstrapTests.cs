using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Activation;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0158f spec-driven multi-repo behaviour: per-repo bootstrap probing,
/// gate error names the missing repos, load handlers populate per-repo
/// dictionaries, AnalyzeProject + PublishLanguage aggregate per-repo,
/// PromptPrefix names repos with their languages.
/// </summary>
public sealed class MultiRepoBootstrapTests
{
    [Fact]
    public async Task BootstrapCheck_AllReposBootstrapped_AllConceptsTrue()
    {
        var harness = new BootstrapHarness()
            .WithRepo("a", contextYaml: true, principles: true)
            .WithRepo("b", contextYaml: true, principles: true);

        await harness.RunCheckAsync();

        var concepts = RunStateConceptsTestFactory.Default(harness.Pipeline);
        concepts.GetBool("context_yaml_present").Should().BeTrue();
        concepts.GetBool("coding_principles_present").Should().BeTrue();
        harness.Pipeline.Get<string>(ContextKeys.MissingBootstrapRepos).Should().BeEmpty();
    }

    [Fact]
    public async Task BootstrapCheck_SomeReposMissing_AggregateFalse_NamesListed()
    {
        var harness = new BootstrapHarness()
            .WithRepo("server", contextYaml: true, principles: true)
            .WithRepo("client", contextYaml: false, principles: true)
            .WithRepo("docs", contextYaml: true, principles: false);

        await harness.RunCheckAsync();

        var concepts = RunStateConceptsTestFactory.Default(harness.Pipeline);
        concepts.GetBool("context_yaml_present").Should().BeFalse();
        concepts.GetBool("coding_principles_present").Should().BeFalse();
        var missing = harness.Pipeline.Get<string>(ContextKeys.MissingBootstrapRepos);
        missing.Should().Contain("client");
        missing.Should().Contain("docs");
        missing.Should().NotContain("server");
    }

    [Fact]
    public async Task BootstrapGate_MissingRepos_ErrorListsRepoNames()
    {
        var harness = new BootstrapHarness()
            .WithRepo("server", contextYaml: true, principles: true)
            .WithRepo("client", contextYaml: false, principles: false);
        await harness.RunCheckAsync();

        var result = await harness.RunGateAsync(pipelineName: "fix-bug");

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("client");
        result.Message.Should().Contain("missing bootstrap");
        result.Message.Should().Contain("init-project");
    }

    [Fact]
    public void PromptPrefix_ListsContextKeys_WithLanguages()
    {
        var section = AgentPromptBuilder.BuildContextsInScopeSection(
            new[] { "server", "client", "docs" },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["server"] = "csharp",
                ["client"] = "typescript",
                ["docs"] = "markdown"
            });

        section.Should().Contain("server (csharp)");
        section.Should().Contain("client (typescript)");
        section.Should().Contain("docs (markdown)");
        section.Should().Contain("context-qualified paths");
        section.Should().Contain("run_command");
    }

    [Fact]
    public void PromptPrefix_SingleContext_EmptySection()
    {
        AgentPromptBuilder.BuildContextsInScopeSection(new[] { "solo" }).Should().BeEmpty();
        AgentPromptBuilder.BuildContextsInScopeSection(null).Should().BeEmpty();
    }

    [Fact]
    public async Task PublishProjectLanguage_MultipleRepos_PublishesPrimaryAndSetsAggregateWhenVocabSupports()
    {
        // p0158f behaviour with a post-p0155 vocab (project_language declared as
        // String, project_languages absent so the handler's catch swallows it).
        // Uses a hand-rolled vocab so the test is invariant under the CI-pinned
        // skill-catalog version — RunStateConceptsTestFactory.Default would
        // pull whatever SKILLS_VERSION CI has pinned, and pre-p0155 catalogs
        // (v2.1.2, v2.2.0) still declare project_language as Enum which would
        // make PublishProjectLanguageHandler.SetString throw.
        var vocab = new ConceptVocabulary(new Dictionary<string, ProjectConcept>
        {
            ["project_language"] = new(
                "project_language", "test", ConceptType.String, null, null, []),
        });
        var conceptsFactory = (PipelineContext ctx) =>
            (IRunStateConcepts)new PipelineContextRunStateConcepts(ctx, vocab);

        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
            {
                ["server"] = NewMap("csharp"),
                ["client"] = NewMap("typescript")
            });

        var handler = new PublishProjectLanguageHandler(
            conceptsFactory,
            NullLogger<PublishProjectLanguageHandler>.Instance);
        var result = await handler.ExecuteAsync(
            new PublishProjectLanguageContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Aggregate is communicated via the message line when count > 1
        result.Message.Should().Contain("project_language=csharp");
        result.Message.Should().Contain("project_languages=csharp,typescript");
    }

    private static ProjectMap NewMap(string primaryLanguage) =>
        new(PrimaryLanguage: primaryLanguage,
            Frameworks: Array.Empty<string>(),
            Modules: Array.Empty<Module>(),
            TestProjects: Array.Empty<TestProject>(),
            EntryPoints: Array.Empty<string>(),
            Conventions: new Conventions(NamingPattern: null, TestLayout: null, ErrorHandling: null),
            Ci: new CiConfig(HasCi: false, BuildCommand: null, TestCommand: null, CiSystem: null));

    private sealed class BootstrapHarness
    {
        public PipelineContext Pipeline { get; } = new();

        private readonly List<RepoConnection> _repos = new();
        private readonly Dictionary<string, Mock<ISandbox>> _sandboxes = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Repo, string Path), bool> _exists = new();
        private readonly Mock<ISandboxFileReaderFactory> _readerFactoryMock = new();

        public BootstrapHarness()
        {
            _readerFactoryMock.Setup(f => f.Create(It.IsAny<ISandbox>()))
                .Returns<ISandbox>(sandbox =>
                {
                    var name = _sandboxes.FirstOrDefault(kv => kv.Value.Object == sandbox).Key ?? string.Empty;
                    var reader = new Mock<ISandboxFileReader>();
                    reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .Returns<string, CancellationToken>((path, _) =>
                            Task.FromResult(_exists.GetValueOrDefault((name, path))));
                    return reader.Object;
                });
        }

        public BootstrapHarness WithRepo(string name, bool contextYaml, bool principles)
        {
            _repos.Add(new RepoConnection { Name = name, Type = RepoType.GitHub, Url = $"https://x/{name}.git" });
            _sandboxes[name] = new Mock<ISandbox>();
            // After p0161a each sandbox is keyed by sandbox-key (= repo name in
            // multi-repo single-context). Bootstrap probes the per-context MetaDir.
            _exists[(name, $"/work/.agentsmith/contexts/default/context.yaml")] = contextYaml;
            _exists[(name, $"/work/.agentsmith/contexts/default/coding-principles.md")] = principles;
            return this;
        }

        public Task<CommandResult> RunCheckAsync()
        {
            Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
            Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes,
                _sandboxes.ToDictionary(kv => kv.Key, kv => kv.Value.Object, StringComparer.Ordinal));
            Pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
                ContextKeys.SandboxDiscoveries,
                _sandboxes.ToDictionary(
                    kv => kv.Key,
                    kv => new RemoteContextDiscovery("default", ".", null),
                    StringComparer.Ordinal));
            var handler = new BootstrapCheckHandler(
                _readerFactoryMock.Object,
                RunStateConceptsTestFactory.Default,
                NullLogger<BootstrapCheckHandler>.Instance);
            return handler.ExecuteAsync(new BootstrapCheckContext(Pipeline), CancellationToken.None);
        }

        public Task<CommandResult> RunGateAsync(string pipelineName)
        {
            Pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig(
                PipelineName: pipelineName,
                Agent: new AgentConfig(),
                SkillsPath: "skills/coding",
                CodingPrinciplesPath: null));
            var handler = new BootstrapGateHandler(
                RunStateConceptsTestFactory.Default,
                AgentSmith.Tests.TestHelpers.EventTestStubs.NoOp,
                NullLogger<BootstrapGateHandler>.Instance);
            return handler.ExecuteAsync(new BootstrapGateContext(Pipeline), CancellationToken.None);
        }
    }
}
