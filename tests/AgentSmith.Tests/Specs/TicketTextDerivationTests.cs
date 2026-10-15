using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.References;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-14-dbcel: a ticket text's run derives its own specs — the hand-off, the gate and the divergence check stand
/// aside for its record — and its derivation sees the cited material first, written into a sandbox and named as files
/// in the opening message. A filed spec's derivation carries nothing early.
/// </summary>
public sealed class TicketTextDerivationTests
{
    private readonly InMemoryFileSandbox _api = new();
    private readonly InMemoryFileSandbox _web = new();
    private readonly HeldReferenceMaterial _held = new();

    [Fact]
    public void SpecSourceResolver_TicketTextRecordAndEmptyPath_DerivesWithTheModel()
    {
        var record = TicketTextMaterialTests.Record([], ticketText: true);
        var ticket = Ticket([FiledTicketLabels.ApprovedSetStamp]);

        var resolver = new SpecSourceResolver(new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance),
            ApprovedSetDoubles.Continuation(), new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
            SeriesSettingsDoubles.Reader(), NullLogger<SpecSourceResolver>.Instance);

        var decision = resolver.Decide(SpecSetOnBranch.Nothing, ticket, null, new PipelineContext(), "github-7",
            TicketLabelVocabulary.Default, record);

        decision.NeedsModel.Should().BeTrue();
        decision.Source.Should().Be(SpecSource.Derived);
        decision.Handback.Should().BeNull("a ticket text's run derives; it is not a missing set");
    }

    [Fact]
    public void FiledTicketSpecGate_StampWithoutRecord_StillParks()
    {
        var gate = new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance);
        var ticket = Ticket([FiledTicketLabels.ApprovedSetStamp]);

        gate.MissingSet(ticket, new TicketKey("github-7"), null, SpecSetBranchState.NothingAtThePath, TicketLabelVocabulary.Default)
            .Should().NotBeNull("a claim before the record exists parks, as for every filing");
        gate.MissingSet(ticket, new TicketKey("github-7"), TicketTextMaterialTests.Record([], ticketText: true),
            SpecSetBranchState.NothingAtThePath, TicketLabelVocabulary.Default).Should().BeNull();
    }

    [Fact]
    public async Task ApprovedSetDivergence_TicketTextRecord_SaysNothing()
    {
        var store = new InMemorySpecApprovalStore();
        var project = new ResolvedProject { Name = "proj", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub } };
        await store.SaveAsync(TicketTextMaterialTests.Record([], ticketText: true) with
        {
            Set = new SpecSet("github-7", [], SpecAccounting.Empty, [], SpecSource.Approved) { Goal = "Something else entirely" },
        }, CancellationToken.None);
        var divergence = new ApprovedSetDivergence(ApprovedSetDoubles.Recorder(store), NullLogger<ApprovedSetDivergence>.Instance);

        (await divergence.ForAsync(project, "7", "A ticket text nothing was approved against", CancellationToken.None))
            .Should().BeNull();
    }

    [Fact]
    public async Task SpecSetDeriver_TicketTextRecord_CarriesTheMaterialBeforeTheModelAndNamesItInTheOpening()
    {
        _held.Set("list", "set-a");
        var client = new Capturing(() => _api.Files.ContainsKey("/work/.agentsmith/reference/set-a/site/index.html"));

        await DeriveAsync(client, TicketTextMaterialTests.Record(["set-a"], ticketText: true, carrying: "sample-api"), ("sample-api", _api));

        client.MaterialWhenAsked.Should().BeTrue("the material is in the sandbox before the model is asked");
        client.Opening.Should().Contain("## Material the approval cites").And.Contain("`.agentsmith/reference/set-a/`");
    }

    [Fact]
    public async Task SpecSetDeriver_FiledSpecRecord_CarriesNothingBeforeTheModel()
    {
        _held.Set("list", "set-a");
        var client = new Capturing(() => _api.Files.Count > 0);

        await DeriveAsync(client, TicketTextMaterialTests.Record(["set-a"], carrying: "sample-api"), ("sample-api", _api));

        client.MaterialWhenAsked.Should().BeFalse("a filed spec's material is carried by the later step, as before");
        client.Opening.Should().NotContain("Material the approval cites");
    }

    [Fact]
    public async Task SpecSetDeriver_TicketTextRecordOverTwoRepos_CarriesIntoTheFirstBeforeTheModel()
    {
        _held.Set("list", "set-a");
        var client = new Capturing(() => _web.Files.ContainsKey("/work/.agentsmith/reference/set-a/site/index.html"));

        await DeriveAsync(client, TicketTextMaterialTests.Record(["set-a"], ticketText: true, repos: ["sample-web", "sample-api"]),
            ("sample-api", _api), ("sample-web", _web));

        client.MaterialWhenAsked.Should().BeTrue();
        _api.Files.Should().BeEmpty("one copy serves every repository");
        client.Opening.Should().Contain("`sample-web/.agentsmith/reference/set-a/`");
    }

    [Fact]
    public async Task SpecSetDeriver_TicketTextRecordWithImages_NamesTheImageFilesInTheOpening()
    {
        _held.Image("list", "img-1");
        var client = new Capturing(() => true);

        await DeriveAsync(client, TicketTextMaterialTests.Record([], ["img-1"], ticketText: true, carrying: "sample-api"), ("sample-api", _api));

        client.Opening.Should().Contain("`.agentsmith/reference/images/img-1.png`").And.NotContain("attached to this message");
    }

    [Fact]
    public async Task SpecSetDeriver_TicketTextRecordWithNoSandboxOfItsRepositories_DerivesWithoutTheMaterial()
    {
        _held.Set("list", "set-a");
        var client = new Capturing(() => _api.Files.Count + _web.Files.Count > 0);

        await DeriveAsync(client, TicketTextMaterialTests.Record(["set-a"], ticketText: true, repos: ["sample-docs", "sample-ops"]),
            ("sample-api", _api), ("sample-web", _web));

        client.Calls.Should().BeGreaterThan(0, "the derivation still runs");
        client.MaterialWhenAsked.Should().BeFalse();
        client.Opening.Should().NotContain("Material the approval cites");
    }

    private async Task DeriveAsync(Capturing client, SpecApprovalRecord record, params (string Repo, ISandbox Sandbox)[] sandboxes)
    {
        var pipeline = TicketTextMaterialTests.Pipeline(record, sandboxes);
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)new Dictionary<string, RemoteContextDiscovery>());
        var deriver = new SpecSetDeriver(
            CutReviewSteps.Of(new CleanReviewer()),
            new SpecDerivationCall(new CutReviewTestDoubles.CappingFactory(client), new AsyncLocalRunContextAccessor(), DerivationTestLooks.Proof()),
            DerivationTestLooks.Factory(), new FixedPrompt(), DerivationTestParsers.Real(), new ScopedContextCoverage(),
            HandbackTestDoubles.Gate(), SeriesSettingsDoubles.Reader(), ReferenceCarryDoubles.Materializer(_held), NullLogger<SpecSetDeriver>.Instance);
        await deriver.DeriveAsync(Ticket([]), TicketSegmenter.Segment("Build the page."), previous: null, series: "2026-10-14-0a0a",
            cause: "initial derivation", repositories: [], new AgentConfig(), pipeline, CancellationToken.None);
    }

    private static Ticket Ticket(IReadOnlyList<string> labels) =>
        new(new TicketId("7"), "page", "Build the page.", null, "open", "github", labels);

    private sealed class CleanReviewer : ISpecCutReviewer
    {
        public Task<SpecCutReview> ReviewAsync(
            IReadOnlyList<SpecDraft> drafts, string key, string? ticketText, DerivationLook? look,
            AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken) => Task.FromResult(SpecCutReview.Clean);
    }

    private sealed class FixedPrompt : IPromptCatalog
    {
        public string Get(string name) => "cut the ticket";

        public string Render(string name, IReadOnlyDictionary<string, string> tokens) => Get(name);
    }

    /// <summary>Records the opening user message and whether the material was in place when the model was first asked.</summary>
    private sealed class Capturing(Func<bool> materialInPlace) : IChatClient
    {
        public int Calls { get; private set; }
        public bool MaterialWhenAsked { get; private set; }
        public string Opening { get; private set; } = string.Empty;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
        {
            if (Calls++ == 0)
            {
                MaterialWhenAsked = materialInPlace();
                Opening = messages.First(m => m.Role == ChatRole.User).Text;
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not a cut")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
