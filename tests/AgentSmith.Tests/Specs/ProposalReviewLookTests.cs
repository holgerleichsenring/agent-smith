using System.Text.Json;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042ed: the look a design turn's proposal is reviewed with — over the turn's own
/// repositories, without the audit, and never concluding from a step a read-only scope refused.
/// </summary>
public sealed class ProposalReviewLookTests
{
    private const string Repo = "repo-a";
    private const string Template = "template:server";

    [Fact]
    public void ReviewLook_ListsTheTurnsRepositories_NotItsTemplates()
    {
        var look = Factory().ForProposalReview(Turn(new RecordingScope(), new RecordingScope()));

        look!.Repositories.Should().Equal([Repo],
            "a template says how work is done, not what this turn assumes");
        look.Templates.Should().BeEmpty();
        look.Tools.Select(t => t.Name).Should().Equal(RepositorySearchTool.Name, RepositoryFileReadTool.Name);
        look.Terms.EvidencePrefix.Should().Be("P");
    }

    [Fact]
    public void ReviewLook_ATurnWithNoRepository_IsNoLook()
    {
        Factory().ForProposalReview(new PipelineContext()).Should().BeNull();
        Factory().ForProposalReview(Turn(new RecordingScope(), template: null)).Should().NotBeNull();
    }

    [Fact]
    public void ReviewLook_Prompt_OffersTheToolsItCarries()
    {
        var drafts = new[] { new AgentSmith.Contracts.Models.PhaseDraft("p9999", "widget goal", "phase: p9999", []) };

        var review = SpecCutReviewPrompt.For(drafts, ticketText: null,
            Factory().ForProposalReview(Turn(new RecordingScope(), template: null)));
        var derivation = SpecCutReviewPrompt.For(
            drafts, ticketText: null, DerivationTestLooks.Over(new CountingSandbox(0)));

        review.Should().Contain("a search and a file read",
            "the prompt lists the tools the look carries, not all three");
        derivation.Should().Contain("the ecosystem's own dependency audit");
    }

    [Fact]
    public async Task ReviewLook_SearchOnReadOnlyScope_RunsAsAGrepStep()
    {
        var scope = new RecordingScope { Grep = [Match("src/Api.cs", 12, "app.MapGet(\"/widget\")")] };
        var (search, look) = Search(scope);

        var found = await search.SearchRepository(Repo, "MapGet");

        scope.Ran.Should().OnlyContain(step => step.Kind == StepKind.Grep || step.Kind == StepKind.ReadFile,
            "a Run step on a source scope is refused with exit 1, which reads as 'found nothing'");
        found.Should().Contain("found in").And.Contain("src/Api.cs:12");
        look.Evidence.Looks.Single().Ran.Should().BeTrue();
    }

    [Fact]
    public async Task ReviewLook_SearchThatMatchesNothing_IsAnAbsenceNotAFailure()
    {
        var (search, look) = Search(new RecordingScope());

        var found = await search.SearchRepository(Repo, "MapGet");

        found.Should().Contain("does not occur anywhere");
        look.Evidence.Looks.Single().Ran.Should().BeTrue();
    }

    [Fact]
    public async Task ReviewLook_SearchWithInvalidPatternOrMissingPath_IsCouldNotRun()
    {
        var (search, look) = Search(new RecordingScope { StepExit = 1, Error = "path not found: /work/nope" });

        var found = await search.SearchRepository(Repo, "MapGet", "nope");

        found.Should().Contain("proves nothing");
        look.Evidence.Looks.Single().Ran.Should().BeFalse();
        look.Evidence.Lines.Single().Should().Contain("could not run, so it proves nothing");
    }

    [Fact]
    public async Task ReviewLook_UnmaterialisableScope_MintsNothingAsProof()
    {
        var scope = new RecordingScope { OpenFailure = "Repo 'repo-a' has no clone URL configured" };
        var (search, look) = Search(scope);
        var read = new RepositoryFileReadTool(look, new FixedReaderFactory(new InMemorySandboxFileReader()), NullLogger.Instance);

        var searched = await search.SearchRepository(Repo, "MapGet");
        var opened = await read.ReadFile(Repo, "src/Api.cs");

        scope.Ran.Should().BeEmpty("nothing is sent to a scope that could not be opened");
        searched.Should().Contain("could not be opened").And.Contain("proves nothing");
        opened.Should().Contain("could not be read").And.Contain("proves nothing");
        look.Evidence.Looks.Should().OnlyContain(l => !l.Ran);
    }

    [Fact]
    public async Task ReviewLook_ReadOfAMissingFile_IsAnAbsenceTheReviewerMayState()
    {
        var scope = new RecordingScope { StepExit = 1, Error = "file not found: /work/src/Gone.cs" };
        var look = Over(scope);
        var read = new RepositoryFileReadTool(look, new FixedReaderFactory(new InMemorySandboxFileReader()), NullLogger.Instance);

        var missing = await read.ReadFile(Repo, "src/Gone.cs");

        missing.Should().Contain("does not exist.");
        look.Evidence.Looks.Single().Ran.Should().BeTrue("a file that is not there is a fact about the tree");
    }

    [Fact]
    public async Task DerivationLook_RefusedStepOnATemplate_IsNotEvidenceOfAbsence()
    {
        var template = new RecordingScope { RefuseRuns = true };
        var look = new DerivationLook(
            new Dictionary<string, ISandbox> { [Repo] = new CountingSandbox(0) },
            new FixedReaderFactory(new InMemorySandboxFileReader()), new PackageEcosystemDetector(),
            NullLogger.Instance, new Dictionary<string, ISourceScopeSandbox> { [Template] = template });

        var searched = await new RepositorySearchTool(look, NullLogger.Instance)
            .SearchRepository(Template, "MapGet");

        template.Ran.Should().ContainSingle().Which.Kind.Should().Be(StepKind.Grep,
            "a Run grep on a source scope is REFUSED with exit 1, which its own convention reads "
            + "as 'the pattern is absent' — the refusal itself standing as the proof");
        searched.Should().Contain("does not occur anywhere", "the Grep step answered, and it answered nothing");
        look.Evidence.Looks.Single().Ran.Should().BeTrue();
        look.Evidence.Looks.Single().What.Should().StartWith("rg ",
            "the line names the engine that ran, not the one the other branch would have used");
    }

    // 2026-09-17-042ed: inside ONE derivation both branches are live — a command sandbox runs a
    // POSIX-ERE `grep -E`, a read-only source scope runs ripgrep under different filters. An
    // evidence line that spelled one command for both would be a fact about a run that never
    // happened.
    [Fact]
    public async Task ReviewLook_TheEvidenceLine_NamesTheEngineThatActuallyRan()
    {
        var overScope = Over(new RecordingScope());
        var overCommandSandbox = Over(new CountingSandbox(1));

        await new RepositorySearchTool(overScope, NullLogger.Instance).SearchRepository(Repo, "MapGet");
        await new RepositorySearchTool(overCommandSandbox, NullLogger.Instance).SearchRepository(Repo, "MapGet");

        overScope.Evidence.Looks.Single().What.Should().Be("rg --hidden --no-ignore 'MapGet' .");
        overCommandSandbox.Evidence.Looks.Single().What.Should().Be("grep -E 'MapGet' .");
    }

    // The review's own search is the ONE that reads dotfiles and ignored paths: an absence it
    // states must hold over the whole checkout. Every other grep keeps the repository's rules.
    [Fact]
    public async Task ReviewLook_Search_AsksForHiddenAndIgnoredPaths()
    {
        var scope = new RecordingScope();
        var (search, _) = Search(scope);

        await search.SearchRepository(Repo, "MapGet");

        scope.Ran.Should().ContainSingle().Which.SearchHidden.Should().BeTrue();
    }

    // A blinkered search that reports "absent" is admitted as proof, so the blinkers are named
    // where the model reads what the tool does.
    [Fact]
    public void ReviewLook_SearchDescription_NamesWhatIsNotSearched()
    {
        var description = new RepositorySearchTool(Over(new RecordingScope()), NullLogger.Instance).Description;

        foreach (var skipped in GrepScope.ExcludedDirs) description.Should().Contain(skipped);
        description.Should().Contain("1 MB").And.Contain("NOT exhaustive");
        GrepScope.MaxFileSizeBytes.Should().Be(1_000_000, "the description says 1 MB in words");
    }

    // 2026-09-17-042ed: the scope wraps ANY post-open failure into an exit 1 whose text is the
    // reason, so a failure that merely CONTAINS the words "not found" must not be laundered into
    // "the file is not there" — an absence the reviewer is then free to state as a fact.
    [Fact]
    public async Task ReviewLook_AFailureThatMerelyMentionsNotFound_IsNotAnAbsence()
    {
        var scope = new RecordingScope { StepExit = 1, Error = "fatal: remote branch not found" };
        var look = Over(scope);
        var read = new RepositoryFileReadTool(look, new FixedReaderFactory(new InMemorySandboxFileReader()), NullLogger.Instance);

        var answer = await read.ReadFile(Repo, "src/Api.cs");

        answer.Should().Contain("could not be read").And.Contain("proves nothing");
        look.Evidence.Looks.Single().Ran.Should().BeFalse();
    }

    [Fact]
    public async Task DerivationLook_AuditOnATemplate_IsCouldNotRun()
    {
        var template = new RecordingScope { RefuseRuns = true };
        var files = new InMemorySandboxFileReader();
        files.Files["/work/package.json"] = "{}";
        var look = new DerivationLook(
            new Dictionary<string, ISandbox> { [Repo] = new CountingSandbox(0) },
            new FixedReaderFactory(files), new PackageEcosystemDetector(),
            NullLogger.Instance, new Dictionary<string, ISourceScopeSandbox> { [Template] = template });

        var audited = await new DependencyAuditTool(
            look, new FixedReaderFactory(files), new PackageEcosystemDetector(), NullLogger.Instance)
            .AuditDependencies(Template);

        template.Ran.Should().NotContain(step => step.Kind == StepKind.Run,
            "a refused audit exits 1, which this tool's own convention reads as 'no findings'");
        audited.Should().Contain("proves nothing");
        look.Evidence.Looks.Single().Ran.Should().BeFalse();
    }

    private static (RepositorySearchTool Search, DerivationLook Look) Search(ISourceScopeSandbox scope)
    {
        var look = Over(scope);
        return (new RepositorySearchTool(look, NullLogger.Instance), look);
    }

    private static DerivationLook Over(ISandbox scope) =>
        new(new Dictionary<string, ISandbox> { [Repo] = scope },
            new FixedReaderFactory(new InMemorySandboxFileReader()), new PackageEcosystemDetector(),
            NullLogger.Instance, templates: null, DerivationLookTerms.ProposalReview, audits: false);

    private static PipelineContext Turn(ISourceScopeSandbox repo, ISourceScopeSandbox? template)
    {
        var sandboxes = new Dictionary<string, ISandbox> { [Repo] = repo };
        if (template is not null) sandboxes[Template] = template;
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)sandboxes);
        return pipeline;
    }

    private static string Match(string path, int line, string text) =>
        JsonSerializer.Serialize(new { path, line, text, kind = "match" });

    /// <summary>A read-only source scope as the product has one: Run and WriteFile are refused,
    /// a Grep step answers with the rows it was given, and opening may fail.</summary>
    private sealed class RecordingScope : ISourceScopeSandbox
    {
        public List<Step> Ran { get; } = [];
        public IReadOnlyList<string> Grep { get; init; } = [];
        public string? OpenFailure { get; init; }
        public int StepExit { get; init; }
        public string? Error { get; init; }
        public bool RefuseRuns { get; init; }

        public string RepoName => Repo;
        public bool IsMaterialized => OpenFailure is null;
        public string? ResolvedSha => "sha";
        public string JobId => "scope";

        public Task<string> MaterializeAsync(CancellationToken ct) => OpenFailure is null
            ? Task.FromResult("sha")
            : throw new InvalidOperationException(OpenFailure);

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran.Add(step);
            if (RefuseRuns && SourceScopeRefusal.Unless(step, SourceScopeWritePolicy.Nothing) is { } refused)
                return Task.FromResult(refused);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, StepExit, false, 0.1, Error,
                step.Kind == StepKind.Grep ? $"[{string.Join(",", Grep)}]" : "content"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
