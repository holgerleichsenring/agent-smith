using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
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
    public void PromptPrefix_ListsAvailableRepos_WithLanguages()
    {
        var section = AgentPromptBuilder.BuildReposInScopeSection(
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
        section.Should().Contain("repo-qualified paths");
        section.Should().Contain("run_command");
    }

    [Fact]
    public void PromptPrefix_SingleRepo_EmptySection()
    {
        AgentPromptBuilder.BuildReposInScopeSection(new[] { "solo" }).Should().BeEmpty();
        AgentPromptBuilder.BuildReposInScopeSection(null).Should().BeEmpty();
    }

    [Fact]
    public void PublishProjectLanguage_MultipleRepos_PublishesPrimaryAndSetsAggregateWhenVocabSupports()
    {
        // The current pinned skill catalog doesn't declare project_languages yet
        // (catalog bump separate from p0158f). PublishProjectLanguage publishes
        // primary project_language to the concept vocabulary; the aggregate
        // attempt is silently swallowed when the concept isn't in the vocab.
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps,
            new Dictionary<string, ProjectMap>(StringComparer.Ordinal)
            {
                ["server"] = NewMap("csharp"),
                ["client"] = NewMap("typescript")
            });

        var handler = new PublishProjectLanguageHandler(
            RunStateConceptsTestFactory.Default,
            NullLogger<PublishProjectLanguageHandler>.Instance);
        var result = handler.ExecuteAsync(
            new PublishProjectLanguageContext(pipeline), CancellationToken.None).Result;

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
            _exists[(name, $"{Repository.SandboxWorkPath}/{ProjectMetaPaths.ContextYaml}")] = contextYaml;
            _exists[(name, $"{Repository.SandboxWorkPath}/{ProjectMetaPaths.CodingPrinciples}")] = principles;
            return this;
        }

        public Task<CommandResult> RunCheckAsync()
        {
            Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, _repos);
            Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes,
                _sandboxes.ToDictionary(kv => kv.Key, kv => kv.Value.Object, StringComparer.Ordinal));
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
                NullLogger<BootstrapGateHandler>.Instance);
            return handler.ExecuteAsync(new BootstrapGateContext(Pipeline), CancellationToken.None);
        }
    }
}
