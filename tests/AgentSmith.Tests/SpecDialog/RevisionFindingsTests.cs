using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9 left this unpinned when the edit loop went: an ordinary message after a proposal runs a turn that
/// revises the AWAITING proposal, and that turn's prompt carries what the proposal's review found.
/// </summary>
public sealed class RevisionFindingsTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Dialog = "d-rev";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;

    public RevisionFindingsTests()
    {
        _connection.Open();
        _context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
        _repository = new SpecDialogSessionRepository(_context);
    }

    [Fact]
    public async Task RevisionTurn_AwaitingProposalWithFindings_SeesTheFindings()
    {
        var state = await OpenAsync();
        var awaiting = new BugOutcome(new BugTicketDraft("The widget is lost", "It vanished.", null))
        {
            Findings = [new ProposalFinding("bug", "unverified", "nothing shows where the widget is drawn")],
        };
        await new SpecDialogDecisionHarness(_context).Proposals.StoreAwaitingAsync(state, awaiting, CancellationToken.None);
        var continuity = new SpecDialogProposalContinuity(Mock.Of<IServiceScopeFactory>(),
            new AsyncLocalTurnProposalObserverAccessor(),
            new SpecDialogLatestOutcomeStore(_repository, BoundSessionNudges.Quiet(_repository), NullLogger<SpecDialogLatestOutcomeStore>.Instance),
            NullLoggerFactory.Instance);

        var revising = await continuity.ContinuingAsync(state, CancellationToken.None);
        var pipeline = new PipelineContext();
        foreach (var (key, value) in SpecDialogTurnSeeds.Build(revising, [new RepoConnection { Name = "repo-a" }],
                     [], new SpecDialogReplySlot(), DialogImageSet.None, new SpecDialogTurnFacts(Mock.Of<IFiledTicketWithdrawal>())))
            pipeline.Set(key, value);

        SpecDialogRevisionSection.Render(pipeline).Should()
            .Contain("What the review of your last proposal found").And.Contain("nothing shows where the widget is drawn");
    }

    private async Task<ConversationState> OpenAsync()
    {
        var sessions = new SpecDialogSessionManager(_repository, BoundSessionNudges.Quiet(_repository),
            AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
        await sessions.OpenAsync(Platform, Dialog, Dialog, "person-a",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);
        return (await sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))!;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
