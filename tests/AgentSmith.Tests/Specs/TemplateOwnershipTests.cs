using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-6f8d: one statement says which declaration OWNS a template address, and the
/// scope selection and the proof report both read it.
/// <para>
/// Two declarations of one address share ONE materialised scope. The report read each of
/// them there — so the second declaration's template-context path was read out of the FIRST
/// declaration's checkout, and that read was minted as a numbered evidence line a finding
/// could cite. A declaration that does not own its address is not read at all now, and says
/// so under its ordinal.
/// </para>
/// </summary>
public sealed class TemplateOwnershipTests
{
    private const string Context = "server";
    private const string Owners = "reference";
    private const string Losers = "sample";
    private const string Repo = "reference-project";

    private const string DeclaresTwo = """
        meta:
          workdir: "."
        verify:
          - label: "build"
            command: "dotnet build"
          - label: "test"
            command: "dotnet test"
        """;

    [Fact]
    public async Task Proof_TwoDeclarationsOfOneAddress_IssueOneReadAndItIsTheOwnersPath()
    {
        var files = Reader(Owners, Losers);
        var (report, look, pipeline) = Build(files, Declared(Owners), Declared(Losers));

        await report.RecordAsync(look, pipeline, CancellationToken.None);

        files.Reads.Should().ContainSingle(
                "the loser shares the owner's checkout, and reading its own template context "
                + "there measures a file that declaration never named")
            .Which.Should().Be(PathOf(Owners));
    }

    [Fact]
    public async Task Proof_TwoContextSpellingsOfOneDiscoveredContext_OpenOneScopeAndAreMeasuredOnce()
    {
        var project = Project(Declared(Owners), Declared(Losers, context: "SERVER"));
        var factory = new CountingScopeFactory();
        var pipeline = Discovered(project);
        var templates = new ProjectTemplateScopes(
            factory, NullLogger<ProjectTemplateScopes>.Instance).For(pipeline);
        var files = Reader(Owners, Losers);
        var (report, look) = Over(files, templates);

        await report.RecordAsync(look, pipeline, CancellationToken.None);

        factory.Created.Should().Be(1, "two spellings of one context name one address");
        templates.Keys.Should().ContainSingle().Which.Should().Be(
            TemplateScopeName.Prefix + Context, "the address keeps the casing it was written with");
        files.Reads.Should().ContainSingle().Which.Should().Be(PathOf(Owners));
    }

    [Fact]
    public async Task Proof_TheSkippedDeclaration_IsRecordedUnderItsOrdinalAndItsTemplateContext()
    {
        var files = Reader(Owners, Losers);
        var (report, look, pipeline) = Build(files, Declared(Owners), Declared(Losers));

        await report.RecordAsync(look, pipeline, CancellationToken.None);

        look.Evidence.Lines.Should().HaveCount(2);
        look.Evidence.Lines[1].Should().Contain("declaration 2")
            .And.Contain($"'{Losers}'", "the ordinal alone names an entity the reader has no list of")
            .And.Contain("could not run, so it proves nothing");
    }

    [Fact]
    public async Task Proof_ThreeIdenticalDeclarations_ProduceThreeDistinguishableLines()
    {
        var files = Reader(Owners);
        var (report, look, pipeline) = Build(
            files, Declared(Owners), Declared(Owners), Declared(Owners));

        await report.RecordAsync(look, pipeline, CancellationToken.None);

        look.Evidence.Lines.Should().HaveCount(3).And.OnlyHaveUniqueItems(
            "two losers identical in every other field are told apart by their ordinal alone");
        look.Evidence.Lines[2].Should().Contain("declaration 3");
    }

    [Fact]
    public async Task Proof_DistinctlyQualifiedTemplates_AreBothStillMeasured()
    {
        var files = Reader(Owners, Losers);
        var (report, look, pipeline) = Build(
            files, Declared(Owners, contextRepo: "Sample.Client"),
            Declared(Losers, contextRepo: "Sample.Worker"));

        await report.RecordAsync(look, pipeline, CancellationToken.None);

        files.Reads.Should().BeEquivalentTo([PathOf(Owners), PathOf(Losers)],
            "two repositories declaring one context name are two addresses, not a duplicate");
        look.Evidence.Lines.Should().HaveCount(2)
            .And.OnlyContain(line => line.Contains("declares 2 verify stage(s)"));
    }

    [Fact]
    public void Scopes_TwoSpellingsOfTheQualifyingRepository_ResolveToOneOwnerAndOpenOneScope()
    {
        var factory = new CountingScopeFactory();
        var project = Project(
            Declared(Owners, contextRepo: "Sample.Client"),
            Declared(Losers, contextRepo: "sample.client"));

        var opened = new ProjectTemplateScopes(
            factory, NullLogger<ProjectTemplateScopes>.Instance).ForProject(project);

        factory.Created.Should().Be(1,
            "the repository a declaration names is a configured ref the catalog compares "
            + "case-insensitively, so two spellings of it are one address — keying the result "
            + "ordinally opened two clones of one repository");
        opened.Keys.Should().ContainSingle().Which.Should().Be(
            TemplateScopeName.Prefix + "Sample.Client/" + Context);
    }

    // ---- composition -------------------------------------------------------

    private static string PathOf(string templateContext) =>
        $"/work/.agentsmith/contexts/{templateContext}/context.yaml";

    private static ProjectTemplate Declared(
        string templateContext, string? contextRepo = null, string context = Context) =>
        new(context, templateContext, "v1.0.0",
            new RepoConnection
            {
                Name = Repo, Type = RepoType.GitHub, Url = "https://stub.test/reference-project",
            },
            contextRepo);

    private static ResolvedProject Project(params ProjectTemplate[] templates) =>
        new() { Name = "sample", Templates = templates };

    private static RecordingReader Reader(params string[] templateContexts)
    {
        var files = new RecordingReader();
        foreach (var context in templateContexts) files.Files[PathOf(context)] = DeclaresTwo;
        return files;
    }

    // One scope per distinct address, as the selection builds it, so a duplicate declaration
    // finds the scope its owner opened — which is the whole situation under test.
    private static (TemplateProofReport Report, DerivationLook Look, PipelineContext Pipeline) Build(
        RecordingReader files, params ProjectTemplate[] templates)
    {
        var scopes = new Dictionary<string, ISourceScopeSandbox>(TemplateScopeName.Comparer);
        foreach (var template in templates)
            scopes.TryAdd(TemplateScopeName.For(template), new OpenScope());
        var (report, look) = Over(files, scopes);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, Project(templates));
        return (report, look, pipeline);
    }

    private static (TemplateProofReport Report, DerivationLook Look) Over(
        RecordingReader files, IReadOnlyDictionary<string, ISourceScopeSandbox> templates)
    {
        var reader = new DerivationTestLooks.FixedReaderFactory(files);
        return (DerivationTestLooks.Proof(reader),
            new DerivationLook(
                new Dictionary<string, ISandbox>(StringComparer.Ordinal)
                { ["Sample.Server"] = new DerivationTestLooks.CountingSandbox(0) },
                reader, new PackageEcosystemDetector(), NullLogger.Instance, templates));
    }

    // A run that checked out one context, spelled as the first declaration spells it.
    private static PipelineContext Discovered(ResolvedProject project)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, project);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            { ["Sample.Server"] = new DerivationTestLooks.CountingSandbox(0) });
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            { ["Sample.Server"] = [new RemoteContextDiscovery(Context, ".", "csharp")] });
        return pipeline;
    }

    private sealed class RecordingReader : ISandboxFileReader
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);
        public List<string> Reads { get; } = [];

        public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
            Task.FromResult(Files.ContainsKey(path));

        public Task<string?> TryReadAsync(string path, CancellationToken ct)
        {
            Reads.Add(path);
            return Task.FromResult(Files.TryGetValue(path, out var content) ? content : null);
        }

        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) =>
            throw new NotSupportedException("the proof reads with TryRead");

        public Task WriteAsync(string path, string content, CancellationToken ct) =>
            throw new NotSupportedException("the proof writes nothing");

        public Task<IReadOnlyList<string>> ListAsync(string path, int? depth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class CountingScopeFactory : ISourceScopeSandboxFactory
    {
        public int Created { get; private set; }

        public ISourceScopeSandbox Create(
            ResolvedProject project, RepoConnection repo, string? revision = null)
        {
            Created++;
            return new OpenScope();
        }
    }

    /// <summary>A template scope the model already opened — the only state the read needs.</summary>
    private sealed class OpenScope : ISourceScopeSandbox
    {
        public string RepoName => Repo;
        public bool IsMaterialized => true;
        public string? ResolvedSha => "abc123";
        public string JobId => "template-scope";

        public Task<string> MaterializeAsync(CancellationToken ct) =>
            throw new InvalidOperationException("the report materialises nothing");

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? p, CancellationToken ct) =>
            Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, string.Empty));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
