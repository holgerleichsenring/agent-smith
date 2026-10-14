using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Tickets.TicketConversationFixtures;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-13-9a59h: the bound Work it out conversation in a ticket's conversation — found without the tracker,
/// shown behind dialog.write on an enforcing install, and the hiding stated rather than left to be guessed.
/// </summary>
public sealed class TicketConversationTranscriptTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public TicketConversationTranscriptTests() =>
        _h.Tracker.Setup(t => t.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<TicketComment>());

    public void Dispose() => _h.Dispose();

    private static readonly SpecOutcome Proposal = new(new SpecDraft("2026-10-13-1a2b", "Export in a second.", "spec: x\n", [])
        { Repos = ["api"], Done = ["It exports in a second."] });

    private async Task<TicketConversationView> EndpointAsync(TokenAuthorityConfig auth, params string[] permissions)
    {
        var services = new ServiceCollection().AddSingleton(auth).AddSingleton(ResolverUnderTest.With(auth));
        var ctx = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = ResolverUnderTest.Caller(auth, [.. permissions.Select(p => (PermissionClaims.Type, p))]),
        };
        var result = await TicketConversationReadEndpoints.ConversationAsync(
            "alpha", "42", ctx, _h.Conversation, CancellationToken.None);
        return result.Should().BeOfType<Ok<TicketConversationView>>().Subject.Value!;
    }

    private Task SeedAsync(OutcomeProposal? proposal = null) => _h.AddAsync(
        Attempt("r-1", 0), Session(proposal, Turn(TranscriptRole.User, "Can it be faster?", 90), Turn(TranscriptRole.Assistant, "Yes.", 91)));

    [Fact]
    public async Task TicketConversation_WithoutDialogWrite_TranscriptHiddenTrue()
    {
        await SeedAsync(Proposal);

        var view = await EndpointAsync(new TokenAuthorityConfig { Enforce = true }, Permissions.RunsRead);

        view.TranscriptHidden.Should().BeTrue();
        view.Entries.Should().NotContain(e => e.Source == TicketConversationSources.Conversation);
        (view.Proposal, view.SessionId).Should().Be(((TicketConversationProposalView?)null, (string?)null));
    }

    [Fact]
    public async Task TicketConversation_NonEnforcing_TranscriptShown()
    {
        await SeedAsync(Proposal);

        var view = await EndpointAsync(new TokenAuthorityConfig { Enforce = false });

        view.TranscriptHidden.Should().BeFalse();
        view.Entries.Where(e => e.Source == TicketConversationSources.Conversation)
            .Select(e => (e.Role, e.Author, e.Group)).Should().Equal(
                (TicketConversationRoles.Person, null, null), (TicketConversationRoles.Agent, null, null));
        view.Proposal!.Drafts.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new TicketConversationDraftView("2026-10-13-1a2b", "Export in a second.", ["api"], ["It exports in a second."]));
        (view.SessionId, view.OpenDialogId).Should().Be(("s-1", "d-1"));
    }

    // 2026-10-14-da5eb: a turn that recorded its author shows the name; an older one stays role-named.
    [Fact]
    public async Task TicketConversation_TurnWithAuthor_ShowsTheName()
    {
        await _h.AddAsync(Session(null,
            Turn(TranscriptRole.User, "Older.", 4), Turn(TranscriptRole.User, "Faster?", 5) with { Author = "Ada Lovelace" }));

        var view = await _h.Conversation.ReadAsync("alpha", "42", true, default);

        view!.Entries.Select(e => (e.Role, e.Author)).Should().Equal(
            (TicketConversationRoles.Person, null), (TicketConversationRoles.Person, "Ada Lovelace"));
    }

    [Fact]
    public async Task TicketConversation_NoBoundSession_TranscriptHiddenFalse()
    {
        await _h.AddAsync(Attempt("r-1", 0));

        var view = await EndpointAsync(new TokenAuthorityConfig { Enforce = true }, Permissions.RunsRead);

        (view.TranscriptHidden, view.SessionId).Should().Be((false, (string?)null));
    }

    [Fact]
    public async Task TicketConversation_ProposalCleared_ProposalNull()
    {
        await SeedAsync(Proposal);
        (await _h.Conversation.ReadAsync("alpha", "42", true, default))!.Proposal.Should().NotBeNull();

        await using (var scope = _h.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<AgentSmith.Infrastructure.Persistence.Contracts.IUnitOfWork>();
            await uow.Set<SpecDialogSession>().ExecuteUpdateAsync(s => s.SetProperty(x => x.LatestProposalJson, (string?)null));
        }

        var view = (await _h.Conversation.ReadAsync("alpha", "42", true, default))!;
        (view.Proposal, view.TranscriptHidden).Should().Be(((TicketConversationProposalView?)null, false));
    }

    [Fact]
    public async Task TicketConversation_BoundSession_FoundWithoutTrackerRead()
    {
        await _h.AddAsync(Session(null, Turn(TranscriptRole.User, "Before any run.", 5)));

        var view = await _h.Conversation.ReadAsync("alpha", "42", true, default);

        view!.Entries.Should().ContainSingle().Which.Group.Should().Be(TicketConversationGroups.Before);
        _h.Tracker.Verify(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TicketConversation_UnknownTicketOrProject_IsNotFound()
    {
        var result = await TicketConversationReadEndpoints.ConversationAsync("alpha", "42",
            new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(new TokenAuthorityConfig()).BuildServiceProvider() },
            _h.Conversation, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
        (await _h.Conversation.ReadAsync("gone", "42", true, default)).Should().BeNull();
    }
}
