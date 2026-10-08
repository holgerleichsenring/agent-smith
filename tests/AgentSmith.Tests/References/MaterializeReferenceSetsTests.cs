using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-01-283df: the step before PhaseSequence writes every cited set into the carrying
/// repository at .agentsmith/reference/&lt;setId&gt;/, excluded from git, publishes them for the
/// prompt — and fails the run, naming the sets, when this process cannot read them.
/// </summary>
public sealed class MaterializeReferenceSetsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly InMemoryFileSandbox _repo = new();
    private readonly InMemoryFileSandbox _other = new();
    private readonly Sets _sets = new();

    [Fact]
    public async Task MaterializeReferenceSets_TwoSetsNamedAlike_LandInTwoDirectories()
    {
        _sets.Add("set-a", ("site/index.html", "<h1>A</h1>"), ("site/css/a.css", "h1 { color: #c0ffee; }"));
        _sets.Add("set-b", ("site/index.html", "<h1>B</h1>"));
        var pipeline = Pipeline(["set-a", "set-b"]);

        var result = await Handler(_sets).ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        Text(_repo, "/work/.agentsmith/reference/set-a/site/index.html").Should().Be("<h1>A</h1>");
        Text(_repo, "/work/.agentsmith/reference/set-b/site/index.html").Should().Be("<h1>B</h1>");
        _other.Files.Should().BeEmpty("the sets go to the CARRYING repository only");
        Text(_repo, "/work/.git/info/exclude").Should().Contain("/.agentsmith/reference/");
        var carried = pipeline.Get<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets);
        carried.Select(c => (c.Name, c.Address, c.Path, c.Repo, c.Session)).Should().Equal(
            ("site", "reference:site", ".agentsmith/reference/set-a", "sample-api", "s-1"),
            ("site", "reference:site-2", ".agentsmith/reference/set-b", "sample-api", "s-1"));
        CommandStepClasses.IsNoOpSummary(CommandNames.MaterializeReferenceSets, result.Message).Should().BeFalse();
    }

    [Fact]
    public async Task MaterializeReferenceSets_NoStoreButCitedSets_FailsNamingThem()
    {
        var result = await Handler(new NoReferenceSetReader())
            .ExecuteAsync(new MaterializeReferenceSetsContext(Pipeline(["set-a", "set-b"])), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("set-a").And.Contain("set-b").And.Contain("no reference store");
        _repo.Files.Should().BeEmpty("a set that cannot be read writes nothing — not even the exclusion");
    }

    [Fact]
    public async Task MaterializeReferenceSets_ApprovalCitesNone_IsSilent()
    {
        var pipeline = Pipeline([]);

        var result = await Handler(_sets).ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        CommandStepClasses.IsNoOpSummary(CommandNames.MaterializeReferenceSets, result.Message).Should().BeTrue();
        pipeline.Has(ContextKeys.ReferenceSets).Should().BeFalse();
    }

    [Fact]
    public void CodePreset_MaterializeReferenceSets_RunsDirectlyBeforePhaseSequence()
    {
        var code = PipelinePresets.Code.ToList();

        code.IndexOf(CommandNames.MaterializeReferenceSets).Should().Be(code.IndexOf(CommandNames.PhaseSequence) - 1);
    }

    [Fact]
    public async Task ReferenceGitExclusion_SecondCall_AddsTheLineOnce()
    {
        _repo.Files["/work/.git/info/exclude"] = Encoding.UTF8.GetBytes("# local\n*.log");
        var exclusion = new ReferenceGitExclusion(new SandboxFileReaderFactory());

        await exclusion.EnsureAsync(_repo, CancellationToken.None);
        await exclusion.EnsureAsync(_repo, CancellationToken.None);

        Text(_repo, "/work/.git/info/exclude").Should().Be("# local\n*.log\n/.agentsmith/reference/\n");
    }

    // 2026-10-02-075dd: a carried set brings its note as it stands when the run begins.
    [Fact]
    public async Task ReferenceSetCarrier_CarriesTheLiveNote()
    {
        _sets.Add("set-a", ("app/app.py", "print(1)"));
        var pipeline = Pipeline(["set-a"]);

        await Handler(_sets, new FixedNotes("set-a", "python3 app/app.py"))
            .ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        pipeline.Get<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets).Single().Note.Should().Be("python3 app/app.py");
    }

    private sealed class FixedNotes(string setId, string note) : IReferenceNotes
    {
        public Task<IReadOnlyDictionary<string, string>> NotesAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string> { [setId] = note });

        public Task<bool> SetAsync(string sessionId, string set, string text, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private MaterializeReferenceSetsHandler Handler(IReferenceSetReader reader, IReferenceNotes? notes = null)
    {
        var files = new SandboxFileReaderFactory();
        return new MaterializeReferenceSetsHandler(ApprovedSetDoubles.Resolver(),
            new ReferenceSetCarrier(reader, new ReferenceSetMaterialiser(reader, files, new SandboxBinaryFileWriter()), new ReferenceGitExclusion(files), notes),
            NullLogger<MaterializeReferenceSetsHandler>.Instance);
    }

    private PipelineContext Pipeline(IReadOnlyList<string> cited)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ApprovedSpecSet, SpecApprovalJson.Write(new SpecApprovalRecord("github-7",
            new SpecSet("github-7", [], SpecAccounting.Empty, [], SpecSource.Approved,
                Approval: new SpecApproval(Noon, "s-1", "person")),
            ["sample-web", "sample-api"], "gh", "sample-api", "7", cited)));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["sample-web"] = _other, ["sample-api"] = _repo });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["sample-web"] = "sample-web", ["sample-api"] = "sample-api" });
        return pipeline;
    }

    private static string Text(InMemoryFileSandbox sandbox, string path) => Encoding.UTF8.GetString(sandbox.Files[path]);

    private sealed class Sets : IReferenceSetReader
    {
        private readonly Dictionary<string, List<ReferenceSetFile>> _sets = new(StringComparer.Ordinal);

        public void Add(string id, params (string Path, string Text)[] files) =>
            _sets[id] = [.. files.Select(f => new ReferenceSetFile(f.Path, Encoding.UTF8.GetBytes(f.Text)))];

        public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReferenceSetFile>>(sessionId == "s-1" && _sets.TryGetValue(setId, out var f) ? f : []);

        public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([.. _sets.Keys]);
    }
}
