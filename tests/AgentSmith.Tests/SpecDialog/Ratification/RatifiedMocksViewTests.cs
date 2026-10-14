using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.Conversation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Tickets.TicketConversationFixtures;

namespace AgentSmith.Tests.SpecDialog.Ratification;

/// <summary>
/// 2026-10-13-8762t: the ratified mark on both proposal views — the conversation's view names the newest
/// ratified version per spec, the ticket page's proposal draft carries the version of its own spec.
/// </summary>
public sealed class RatifiedMocksViewTests
{
    private static readonly SpecDraft Parent = new("parent", "the parent", "spec: parent", []);
    private static readonly SpecDraft ChildA = new("spec-a", "the first", "spec: spec-a", []);
    private static readonly SpecDraft ChildB = new("spec-b", "the second", "spec: spec-b", []);

    [Fact]
    public async Task SpecDialogView_RatifiedMocks_NewestRatificationPerSpec()
    {
        using var rig = new RatificationRig();
        var session = await rig.OpenAsync();
        await rig.Mocks.AddAsync(session, "spec-a", "<p>1</p>", default);
        await rig.Mocks.AddAsync(session, "spec-a", "<p>2</p>", default);
        await rig.Mocks.AddAsync(session, "spec-b", "<p>1</p>", default);
        await rig.RatifyAsync(session, "spec-a", 2);
        await rig.RatifyAsync(session, "spec-b", 1);
        await rig.RatifyAsync(session, "spec-a", 1);

        var view = await ViewAsync(rig);

        view.RatifiedMocks.Should().BeEquivalentTo(new Dictionary<string, int> { ["spec-a"] = 1, ["spec-b"] = 1 });
    }

    [Fact]
    public async Task SpecDialogView_NothingRatified_HasNoRatifiedMocks()
    {
        using var rig = new RatificationRig();
        var session = await rig.OpenAsync();
        await rig.Mocks.AddAsync(session, "spec-a", "<p>1</p>", default);

        var view = await ViewAsync(rig);

        view.Mocks.Should().ContainSingle();
        view.RatifiedMocks.Should().BeNull("null when no version is ratified");
    }

    [Fact]
    public void TicketTranscriptReader_ProposalDraft_CarriesTheRatifiedMock()
    {
        var session = Session(new EpicOutcome(Parent, [ChildA, ChildB]), Ratified("spec-b", 3, 1));

        var drafts = Reader().Proposal(session)!.Drafts;

        drafts.Select(d => (d.SpecId, d.RatifiedMock)).Should().Equal(("parent", null), ("spec-a", null), ("spec-b", 3));
    }

    [Fact]
    public void TicketTranscriptReader_RatificationTurnAfterProposal_MarksTheDraft()
    {
        var session = Session(new SpecOutcome(ChildA),
            Ratified("spec-a", 1, 1), Turn(TranscriptRole.Assistant, "proposed", 2), Ratified("spec-a", 2, 3));

        Reader().Proposal(session)!.Drafts.Should().ContainSingle().Which.RatifiedMock.Should().Be(2);
        Reader().Proposal(Session(new SpecOutcome(ChildA)))!.Drafts.Single().RatifiedMock.Should().BeNull();
    }

    private static TranscriptTurn Ratified(string specId, int version, int minutes) =>
        Turn(TranscriptRole.User, $"Ratified mock v{version} of {specId}.", minutes) with
        {
            Kind = SpecDialogTurnKind.MockRatified, Mock = new MockRatificationTurn(specId, version, "person-a"),
        };

    /// <summary>The dialog page's read, in a request scope of its own as the server issues it.</summary>
    private static async Task<SpecDialogSessionView> ViewAsync(RatificationRig rig)
    {
        using var scope = rig.Provider.CreateScope();
        var context = (AgentSmithDbContext)scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repository = scope.ServiceProvider.GetRequiredService<SpecDialogSessionRepository>();
        var reader = new SpecDialogViewReader(scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>(),
            new SpecDialogProjectCatalog(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == new AgentSmithConfig())), new SpecDialogPendingQuestions(rig.Gate),
            new SpecDialogLatestOutcomeStore(repository, BoundSessionNudges.Quiet(repository), NullLogger<SpecDialogLatestOutcomeStore>.Instance),
            new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()), rig.Gate,
            new SpecDialogTicketTextRepository(context), TestUploads.Over(context), rig.Mocks);
        return (await reader.ReadAsync(RatificationRig.Thread, default)).Session!;
    }

    private static TicketTranscriptReader Reader() =>
        new(Mock.Of<IServiceScopeFactory>(), NullLogger<TicketTranscriptReader>.Instance);
}
