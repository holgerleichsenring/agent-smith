using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.References;

/// <summary>
/// 2026-10-14-dbcel: a ticket text's material reaches its run, and a later approval keeps the material of the
/// conversation that filed the ticket — each cited id read from the conversation it was uploaded in.
/// </summary>
public sealed class TicketTextMaterialTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private readonly InMemoryFileSandbox _api = new();
    private readonly InMemoryFileSandbox _web = new();
    private readonly HeldReferenceMaterial _held = new();

    [Fact]
    public async Task MaterializeReferenceSets_TicketTextRecord_CarriesTheCitedSets()
    {
        _held.Set("list", "set-a");
        var pipeline = Pipeline(Record(["set-a"], ticketText: true, repos: ["sample-api"], carrying: "sample-api"), ("sample-api", _api));

        var result = await Handler().ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        _api.Files.Should().ContainKey("/work/.agentsmith/reference/set-a/site/index.html");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaterializeReferenceSets_ResumedWithKeysRestoredIntoAFreshSandbox_WritesTheFilesAgain(bool ticketText)
    {
        _held.Set("list", "set-a").Image("list", "img-1");
        var record = Record(["set-a"], ["img-1"], ticketText, ["sample-api"], "sample-api");
        var first = Pipeline(record, ("sample-api", _api));
        await Handler().ExecuteAsync(new MaterializeReferenceSetsContext(first), CancellationToken.None);
        var fresh = new InMemoryFileSandbox();
        var resumed = Pipeline(record, ("sample-api", fresh));
        resumed.Set(ContextKeys.ReferenceSets, first.Get<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets));
        resumed.Set(ContextKeys.ReferenceImages, first.Get<IReadOnlyList<CarriedReferenceImage>>(ContextKeys.ReferenceImages));

        var result = await Handler().ExecuteAsync(new MaterializeReferenceSetsContext(resumed), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        fresh.Files.Should().ContainKey("/work/.agentsmith/reference/set-a/site/index.html")
            .And.ContainKey("/work/.agentsmith/reference/images/img-1.png");
    }

    [Fact]
    public async Task MaterializeReferenceSets_TicketTextOverTwoReposWithNoSingleRepoSpec_CarriesIntoTheFirst()
    {
        _held.Set("list", "set-a");
        var pipeline = Pipeline(Record(["set-a"], ticketText: true, repos: ["sample-web", "sample-api"]),
            ("sample-api", _api), ("sample-web", _web));

        var result = await Handler().ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        _web.Files.Should().ContainKey("/work/.agentsmith/reference/set-a/site/index.html");
        _api.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task MaterializeReferenceSets_FiledSpecOverTwoReposWithNoCarrier_StillRefuses()
    {
        _held.Set("list", "set-a");
        var pipeline = Pipeline(Record(["set-a"], ticketText: false, repos: ["sample-web", "sample-api"]),
            ("sample-api", _api), ("sample-web", _web));

        var result = await Handler().ExecuteAsync(new MaterializeReferenceSetsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse("only a ticket text takes its first repository");
        result.Message.Should().Contain("no spec names the repository");
    }

    [Fact]
    public async Task ReferenceSetCarrier_CarriedForwardSet_ReadFromItsOwnConversation()
    {
        _held.Set("list", "set-a").Set("own", "set-b");
        var record = Record(["set-a", "set-b"], approvedIn: "own") with { CitedFrom = new Dictionary<string, string> { ["set-a"] = "list" } };

        var carry = await ReferenceCarryDoublesCarrier().CarryAsync(record, "sample-api", _api, CancellationToken.None);

        carry.Refusal.Should().BeNull();
        carry.Sets.Select(s => (s.SetId, s.Session)).Should().Equal(("set-a", "list"), ("set-b", "own"));
    }

    [Fact]
    public async Task ReferenceImageCarrier_CarriedForwardImage_ReadFromItsOwnConversation()
    {
        _held.Image("list", "img-1");
        var record = Record([], ["img-1"], approvedIn: "own") with { CitedFrom = new Dictionary<string, string> { ["img-1"] = "list" } };
        var files = new SandboxFileReaderFactory();

        var carried = await new ReferenceImageCarrier(_held, new SandboxBinaryFileWriter(), new ReferenceGitExclusion(files))
            .CarryAsync(record, _api, CancellationToken.None);

        carried.Single().Image.Path.Should().Be(".agentsmith/reference/images/img-1.png");
        _api.Files.Should().ContainKey("/work/.agentsmith/reference/images/img-1.png");
    }

    [Fact]
    public async Task ApprovalCitations_CiteWithPreviousFromAnotherConversation_KeepsItsIdsWithTheirConversation()
    {
        _held.Set("list", "set-a").Image("list", "img-1").Set("own", "set-b");
        var previous = Record(["set-a"], ["img-1"], approvedIn: "list");

        var cited = await Citations().CiteAsync(Record([]), "own", CancellationToken.None, previous);

        cited.CitedSets.Should().Equal("set-b", "set-a");
        cited.CitedImages.Should().Equal("img-1");
        cited.ConversationOf("set-a").Should().Be("list");
        cited.ConversationOf("img-1").Should().Be("list");
        cited.CitedFrom!.Should().NotContainKey("set-b");
    }

    [Fact]
    public async Task ApprovalCitations_CiteWithPreviousFromTheSameConversation_RemovedUploadNotResurrected()
    {
        _held.Set("own", "set-b");
        var previous = Record(["set-a", "set-b"], approvedIn: "own");

        var cited = await Citations().CiteAsync(Record([]), "own", CancellationToken.None, previous);

        cited.CitedSets.Should().Equal("set-b");
        cited.CitedFrom.Should().BeNull();
    }

    [Fact]
    public async Task ApprovalCitations_CiteWithPreviousWhoseSourceUploadWasRemoved_DropsIt()
    {
        _held.Set("list", "set-a").Set("list", "set-c");
        _held.Remove("list", "set-c");
        var previous = Record(["set-a", "set-c"], approvedIn: "list");

        var cited = await Citations().CiteAsync(Record([]), "own", CancellationToken.None, previous);

        cited.CitedSets.Should().Equal("set-a");
    }

    [Fact]
    public async Task ApprovalCitations_SaveRecheck_KeepsForeignIds()
    {
        _held.Set("list", "set-a").Set("own", "set-b").Set("own", "set-gone");
        var store = new InMemorySpecApprovalStore();
        var record = Record(["set-a", "set-b", "set-gone"], approvedIn: "own") with
            { CitedFrom = new Dictionary<string, string> { ["set-a"] = "list" } };
        _held.Remove("own", "set-gone");

        var saved = await new ApprovalCitations(store, _held).SaveAsync(record, "own", CancellationToken.None);

        saved.CitedSets.Should().Equal("set-a", "set-b");
    }

    [Fact]
    public void SpecApprovalJson_CitedFrom_RoundTrips()
    {
        var record = Record(["set-a"]) with { TicketText = true, CitedFrom = new Dictionary<string, string> { ["set-a"] = "list" } };

        var read = SpecApprovalJson.Read(SpecApprovalJson.Write(record))!;

        read.TicketText.Should().BeTrue();
        read.ConversationOf("set-a").Should().Be("list");
        read.ConversationOf("set-z").Should().Be("list", "an id carried forward from nowhere is the approval's own");
    }

    private ApprovalCitations Citations() => new(new InMemorySpecApprovalStore(), _held);

    private ReferenceSetCarrier ReferenceCarryDoublesCarrier()
    {
        var files = new SandboxFileReaderFactory();
        return new ReferenceSetCarrier(_held, new ReferenceSetMaterialiser(_held, files, new SandboxBinaryFileWriter()), new ReferenceGitExclusion(files));
    }

    private MaterializeReferenceSetsHandler Handler() => new(ReferenceCarryDoubles.Materializer(_held));

    internal static SpecApprovalRecord Record(
        IReadOnlyList<string> sets, IReadOnlyList<string>? images = null, bool ticketText = false,
        IReadOnlyList<string>? repos = null, string carrying = "", string approvedIn = "list") =>
        new SpecApprovalRecord("github-7",
            new SpecSet("github-7", [], SpecAccounting.Empty, [], SpecSource.Approved, Approval: new SpecApproval(Noon, approvedIn, "person")),
            repos ?? ["sample-api"], "gh", carrying, "7", sets, images ?? []) { TicketText = ticketText };

    internal static PipelineContext Pipeline(SpecApprovalRecord record, params (string Repo, ISandbox Sandbox)[] sandboxes)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ApprovedSpecSet, SpecApprovalJson.Write(record));
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes.ToDictionary(s => s.Repo, s => s.Sandbox));
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, sandboxes.ToDictionary(s => s.Repo, s => s.Repo));
        return pipeline;
    }
}
