using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-19-4c1f: the spec-dialog grounding step. The pinned design-partner master
/// declares the project context and the coding principles as inputs and renders a heading
/// for each; the preset filled neither, so every design turn read a heading with nothing
/// under it. These tests hold the fix to the two things that made it hard: the grounding
/// has to reach the SCOPE'S repositories remotely, and it has to provision nothing.
/// </summary>
public sealed class SpecDialogGroundingTests
{
    private const string ContextYaml = """
        meta:
          workdir: "."
          project: sample
        stack:
          lang: csharp
        """;

    private const string Principles = "# Principles\n- One responsibility per file.\n";

    [Fact]
    public async Task Dialog_ProjectWithPrinciples_TheMasterPromptCarriesThem()
    {
        var repo = Scripted("repo-a", contextYaml: true, principles: Principles);
        var pipeline = Pipeline(("repo-a", true));

        var result = await Run(pipeline, repo);

        result.IsSuccess.Should().BeTrue();
        DesignPartnerPrompt(pipeline).Should().Contain("## Coding Principles")
            .And.Contain("One responsibility per file.",
                "the heading the master renders had nothing under it before this step existed");
    }

    [Fact]
    public async Task Dialog_ProjectWithContextDocuments_TheMasterPromptCarriesThem()
    {
        var repo = Scripted("repo-a", contextYaml: true, principles: Principles);
        var pipeline = Pipeline(("repo-a", true));

        await Run(pipeline, repo);

        DesignPartnerPrompt(pipeline).Should().Contain("## Project Context")
            .And.Contain("project: sample", "the context.yaml is read remotely, not from a pod");
    }

    [Fact]
    public async Task Dialog_ScopeNarrowedToOneRepo_GroundsOnThatRepoOnly()
    {
        var inScope = Scripted("repo-a", contextYaml: true, principles: Principles);
        var outOfScope = Scripted("repo-b", contextYaml: true, principles: "# Not this one\n");
        // The pipeline knows the PROJECT's repositories; the sandbox map is the turn's scope.
        var pipeline = Pipeline(("repo-a", true), ("repo-b", false));

        var result = await Run(pipeline, inScope, outOfScope);

        result.Message.Should().Contain("1 of 1 scoped repo(s)");
        outOfScope.Reads.Should().BeEmpty("a repository the operator excluded is never read");
        DesignPartnerPrompt(pipeline).Should().NotContain("Not this one");
    }

    [Fact]
    public async Task Dialog_NoPrinciplesFile_IsReportedAsAbsentNotAsUnattempted()
    {
        var repo = Scripted("repo-a", contextYaml: true, principles: null);
        var pipeline = Pipeline(("repo-a", true));

        var result = await Run(pipeline, repo);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("no file: repo-a/.agentsmith/principles.md");
        repo.Reads.Should().Contain(".agentsmith/principles.md")
            .And.Contain(".agentsmith/contexts/default/principles.md",
                "the flat file is probed first and the fan-out follows — absence is a read that answered");
    }

    [Fact]
    public async Task Dialog_ARepositoryWithNoLocation_IsReportedApartFromAFileThatIsMissing()
    {
        var located = Scripted("repo-a", contextYaml: true, principles: null);
        var pipeline = Pipeline(("repo-a", true), ("repo-b", true));
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "repo-a", Url = "https://example.invalid/a.git" },
             new RepoConnection { Name = "repo-b" }]);

        var result = await Run(pipeline, located);

        result.Message.Should().Contain("no location: repo-b");
        result.Message.Should().Contain("no file: repo-a/.agentsmith/principles.md");
        result.Message.Should().NotContain("no location: repo-a");
    }

    [Fact]
    public async Task Dialog_AProviderThatCannotBeReached_ReportsAndDoesNotFailTheTurn()
    {
        var repo = new ScriptedSourceProvider(
            [], new Dictionary<string, string>(), new HashSet<string>(),
            new InvalidOperationException("the remote refused the connection"));
        var pipeline = Pipeline(("repo-a", true));

        var result = await Run(pipeline, ("repo-a", repo));

        result.IsSuccess.Should().BeTrue("a provider outage leaves a conversation that can still answer");
        result.Message.Should().Contain("could not be read: repo-a/.agentsmith/contexts")
            .And.Contain("the remote refused the connection");
        pipeline.TryGet<string>(ContextKeys.CodingPrinciples, out _).Should().BeFalse(
            "nothing was read, so nothing is published as if it had been");
    }

    [Fact]
    public async Task Dialog_AFileThatCannotBeRead_IsReportedApartFromAFileThatIsAbsent()
    {
        var absent = Scripted("repo-a", contextYaml: true, principles: null);
        var refused = Scripted("repo-b", contextYaml: true, principles: null,
            refused: [".agentsmith/principles.md"]);
        var pipeline = Pipeline(("repo-a", true), ("repo-b", true));

        var result = await Run(pipeline, absent, refused);

        result.Message.Should().Contain("no file: repo-a/.agentsmith/principles.md");
        result.Message.Should().Contain("could not be read: repo-b/.agentsmith/principles.md");
        result.Message.Should().NotContain("no file: repo-b/.agentsmith/principles.md",
            "a refused read says nothing about whether the file is there");
    }

    [Fact]
    public async Task Dialog_TheGroundingStep_ProvisionsNoSandbox()
    {
        SandboxRequiringCommands.Contains(CommandNames.GroundSpecDialog).Should().BeFalse(
            "the executor provisions before a sandbox-requiring step and the coordinator then SETS "
            + "the sandbox map — replacing the turn's lazy read-only scopes with source-less pods");
        var repo = Scripted("repo-a", contextYaml: true, principles: Principles);
        var scope = new Mock<ISandbox>(MockBehavior.Strict);
        var pipeline = Pipeline();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "repo-a", Url = "https://example.invalid/a.git" }]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["repo-a"] = scope.Object });

        var result = await Run(pipeline, repo);

        result.IsSuccess.Should().BeTrue();
        scope.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dialog_AfterTheGroundingStep_TheTurnsOwnScopesAreStillTheSandboxes()
    {
        var repo = Scripted("repo-a", contextYaml: true, principles: Principles);
        var scope = new Mock<ISandbox>(MockBehavior.Strict).Object;
        var seeded = (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["repo-a"] = scope };
        var pipeline = Pipeline();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "repo-a", Url = "https://example.invalid/a.git" }]);
        pipeline.Set(ContextKeys.Sandboxes, seeded);

        await Run(pipeline, repo);

        var after = pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        after.Should().BeSameAs(seeded, "the step publishes grounding, never a sandbox map");
        after["repo-a"].Should().BeSameAs(scope);
    }

    // ---- fixtures ----

    private static ScriptedSourceProvider Scripted(
        string repo, bool contextYaml, string? principles, string[]? refused = null)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (contextYaml) files[".agentsmith/contexts/default/context.yaml"] = ContextYaml;
        if (principles is not null) files[".agentsmith/contexts/default/principles.md"] = principles;
        return new ScriptedSourceProvider(
            ["default"], files, new HashSet<string>(refused ?? [], StringComparer.Ordinal));
    }

    private static Task<CommandResult> Run(
        PipelineContext pipeline, params ScriptedSourceProvider[] providers)
    {
        var byRepo = providers
            .Select((p, i) => (Name: RepoNameOf(pipeline, i), Provider: p))
            .ToDictionary(x => x.Name, x => x.Provider, StringComparer.Ordinal);
        return Execute(pipeline, byRepo);
    }

    private static Task<CommandResult> Run(
        PipelineContext pipeline, params (string Repo, ScriptedSourceProvider Provider)[] providers) =>
        Execute(pipeline, providers.ToDictionary(p => p.Repo, p => p.Provider, StringComparer.Ordinal));

    private static Task<CommandResult> Execute(
        PipelineContext pipeline, IReadOnlyDictionary<string, ScriptedSourceProvider> byRepo)
    {
        var reader = new DialogGroundingReader(
            new ScriptedSourceProviderFactory(byRepo),
            new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())),
            NullLogger<DialogGroundingReader>.Instance);
        var handler = new GroundSpecDialogHandler(reader, NullLogger<GroundSpecDialogHandler>.Instance);
        return handler.ExecuteAsync(new GroundSpecDialogContext(pipeline), CancellationToken.None);
    }

    private static string RepoNameOf(PipelineContext pipeline, int index) =>
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)[index].Name;

    /// <summary>A pipeline shaped like one spec-dialog turn: the project's repositories on
    /// ContextKeys.Repos, and the turn's own scope as the sandbox map it seeded.</summary>
    private static PipelineContext Pipeline(params (string Repo, bool InScope)[] repos)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig(PipelinePresets.SpecDialogName, new AgentConfig(), "skills", null));
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("main"), "https://example.invalid/a.git"));
        if (repos.Length == 0) return pipeline;
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [.. repos.Select(r => new RepoConnection
            {
                Name = r.Repo, Url = $"https://example.invalid/{r.Repo}.git",
            })]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            repos.Where(r => r.InScope).ToDictionary(
                r => r.Repo, _ => new Mock<ISandbox>(MockBehavior.Strict).Object, StringComparer.Ordinal));
        return pipeline;
    }

    // The design partner's pinned body renders "{ProjectContextSection}", then the literal
    // "## Coding Principles" heading followed by "{CodingPrinciples}". Both tokens are bound
    // here off the master context the PRODUCTION builder produces, exactly as
    // AgenticMasterHandler binds them — the heading is what this phase stops leaving empty.
    private static string DesignPartnerPrompt(PipelineContext pipeline)
    {
        var master = (AgenticMasterContext)new AgenticMasterContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.AgenticMaster), new ResolvedProject(), pipeline);
        return $"{MasterPromptSections.BuildProjectContextSection(master.ProjectContext)}\n"
               + $"## Coding Principles\n{master.CodingPrinciples}\n";
    }
}
