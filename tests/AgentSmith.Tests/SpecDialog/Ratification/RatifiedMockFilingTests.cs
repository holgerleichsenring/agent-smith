using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Browser;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog.Ratification;

/// <summary>
/// 2026-10-13-8762s: filing writes the ratified version beside the spec under its MINTED id, read from the
/// conversation by the id the proposal gave it, in the repository whose sub-series holds the spec. What
/// cannot be written is a note: no reader, a version gone, or a branch write that failed.
/// </summary>
public sealed class RatifiedMockFilingTests
{
    private const string Conversation = "job-1";
    private const string Key = "azuredevops-1";
    private readonly MemoryMocks _mocks = new();
    private readonly RecordingBranchSources _sources = new();
    private readonly ISpecApprovalStore _store = ApprovedSetDoubles.Store();

    [Fact]
    public async Task Filing_RatifiedMock_WrittenUnderSpecsPlannedWithTheMintedId()
    {
        await _mocks.AddAsync(Conversation, "p9000a", "<p>v1</p>", default);
        await _mocks.AddAsync(Conversation, "p9000a", "<p>v2</p>", default);

        var report = await FileAsync(Ratified("p9000a", 2));

        report.Error.Should().BeNull();
        report.Notes.Should().BeEmpty();
        var spec = (await _store.GetAsync("sample-tracker", Key, default))!.Set.Specs.Single();
        spec.SpecId.Should().NotBe("p9000a", "filing mints the id");
        spec.Draft.Yaml.Should().Contain("ratified_mock: 2");
        _sources.Writes.Single().ContentOf($"{SeriesPaths.Planned}/{spec.SpecId}-mock-v2.html").Should().Be("<p>v2</p>");
    }

    [Fact]
    public async Task Filing_RatifiedMockGoneFromConversation_IsANoteNotAnError()
    {
        await _mocks.AddAsync(Conversation, "p9000a", "<p>v1</p>", default);

        var report = await FileAsync(Ratified("p9000a", 2));

        report.Error.Should().BeNull();
        report.Notes.Should().ContainSingle().Which.Should().Contain("mock v2").And.Contain("was not written");
        _sources.Writes.Single().Paths.Should().NotContain(p => p.EndsWith(".html"));
    }

    [Fact]
    public async Task Filing_BranchWriteFailed_NoteSaysTheMockWasNotWritten()
    {
        await _mocks.AddAsync(Conversation, "p9000a", "<p>v1</p>", default);
        _sources.Sha = null;

        var report = await FileAsync(Ratified("p9000a", 1));

        report.Error.Should().BeNull();
        report.Notes.Should().ContainSingle().Which.Should().Contain("not on its ticket branch")
            .And.Contain("The ratified mock was not written");
    }

    [Fact]
    public async Task FiledSpecBranch_NoMockReader_NotesTheMockWasNotWritten()
    {
        var result = await ApprovedSetDoubles.Branch(_sources).WriteAsync(
            Project(), Record(Spec("2026-10-06-3d3da", "sample-api", 1)), new TicketId("1"), Stored(), default);

        result.Written.Should().BeTrue();
        result.Notes.Should().Equal(RatifiedMockFiles.NoStore);
        _sources.Writes.Single().Paths.Should().NotContain(p => p.EndsWith(".html"));
    }

    [Fact]
    public async Task SeriesWrite_TwoRepositories_EachMockGoesToItsSpecsRepository()
    {
        await _mocks.AddAsync(Conversation, "p-web", "<p>web</p>", default);
        await _mocks.AddAsync(Conversation, "p-api", "<p>api</p>", default);
        var sources = new Dictionary<string, string> { ["2026-10-06-3d3da"] = "p-web", ["2026-10-06-3d3db"] = "p-api" };

        var result = await ApprovedSetDoubles.Branch(_sources, mocks: _mocks).WriteAsync(Project(),
            Record(Spec("2026-10-06-3d3da", "sample-web", 1), Spec("2026-10-06-3d3db", "sample-api", 1)),
            new TicketId("1"), Stored(), default, sources);

        result.Written.Should().BeTrue();
        var html = _sources.Writes.ToDictionary(w => w.Repo, w => w.Files.Where(f => f.Path.EndsWith(".html")).ToList());
        html["sample-web"].Should().ContainSingle().Which.Should().Be(
            new RepoFile($"{SeriesPaths.Planned}/2026-10-06-3d3da-mock-v1.html", "<p>web</p>"));
        html["sample-api"].Should().ContainSingle().Which.Should().Be(
            new RepoFile($"{SeriesPaths.Planned}/2026-10-06-3d3db-mock-v1.html", "<p>api</p>"));
    }

    private Task<FilingReport> FileAsync(SpecDraft draft)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CreatedTicket(new TicketId("1"), "https://tracker.test/1"));
        provider.Setup(p => p.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Stored());
        var factory = Mock.Of<ITicketProviderFactory>(f => f.Create(It.IsAny<TrackerConnection>()) == provider.Object);
        var filer = new OutcomeTicketFiler(
            new AgentSmithConfig { Projects = new Dictionary<string, ResolvedProject> { ["proj"] = Project() } },
            factory, new SpecTicketRenderer(), new BugTicketRenderer(), new EpicChildOrderer(), ApprovedSetDoubles.SeriesFiling(),
            ApprovedSetDoubles.SetFiler(_store, sources: _sources, mocks: _mocks), FiledWorkDoubles.Starter(),
            ApprovedSetDoubles.Kinds(), NullLogger<OutcomeTicketFiler>.Instance);
        var state = new ConversationState
        {
            JobId = Conversation, ChannelId = "C1", UserId = "sample.user", Platform = "dashboard", Project = "proj",
            TicketId = string.Empty, StartedAt = DateTimeOffset.UnixEpoch,
        };
        return filer.FileAsync(state, new SpecOutcome(draft), false, CancellationToken.None);
    }

    private static SpecDraft Ratified(string id, int version) =>
        new(id, "Do the thing", $"spec: {id}\ngoal: \"Do the thing\"\nratified_mock: {version}", [])
        { Done = ["It is done."], Repos = ["sample-api"], RatifiedMock = version };

    private static DerivedSpec Spec(string id, string repo, int version) =>
        new(Ratified(id, version) with { Repos = [repo] }, "do-the-thing", string.Empty, []);

    private static SpecApprovalRecord Record(params DerivedSpec[] specs) =>
        new(Key, new SpecSet(Key, specs, SpecAccounting.Empty, [], SpecSource.Approved,
                Approval: new SpecApproval(DateTimeOffset.UnixEpoch, Conversation, "sample.user"), Series: "2026-10-06-3d3d"),
            ["sample-api", "sample-web"], "sample-tracker", "sample-api");

    private static Ticket Stored() => new(new TicketId("1"), "title", "body", null, "open", "mock", []);

    private static ResolvedProject Project() => new()
    {
        Name = "proj",
        Tracker = new TrackerConnection { Name = "sample-tracker", Type = TrackerType.AzureDevOps },
        Repos = [new RepoConnection { Name = "sample-api" }, new RepoConnection { Name = "sample-web" }],
    };
}
