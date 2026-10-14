using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Conversation;

/// <summary>
/// 2026-10-13-9a59g: a turn or a proposal saved in a ticket-bound design conversation tells that ticket's
/// pages it changed; an unbound conversation tells nobody. Resolved through a container, so the session
/// manager and the pane store reach the nudge the way the server composes them.
/// </summary>
public sealed class BoundSessionNudgeTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Project = "alpha";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly Mock<ITicketActivityNudge> _activity = new();
    private readonly ServiceProvider _provider;

    public BoundSessionNudgeTests()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options));
        services.AddScoped<SpecDialogSessionRepository>();
        services.AddSingleton(_activity.Object);
        services.AddSingleton(Sandbox.Holds.None());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SpecDialogSessionManager>().AddScoped<BoundSessionNudge>();
        services.AddScoped<SpecDialogLatestOutcomeStore>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task BoundSession_TurnSaved_PublishesTicketActivity_UnboundDoesNot()
    {
        var binding = TicketBinding.For("gh", "github", "42", "Widget");
        await OpenAsync("bound", binding);
        await OpenAsync("loose", ticket: null);

        await TurnAsync("bound");
        await TurnAsync("loose");

        _activity.Verify(a => a.ChangedAsync(Project, binding.Key, It.IsAny<CancellationToken>()), Times.Once);
        _activity.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BoundSession_ProposalSaved_PublishesTicketActivity()
    {
        var binding = TicketBinding.For("gh", "github", "42", "Widget");
        await OpenAsync("bound", binding);

        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SpecDialogLatestOutcomeStore>()
            .SetProposalAsync(Platform, "bound", new SpecOutcome(new SpecDraft("p9001", "goal", "spec: p9001", [])), CancellationToken.None);

        _activity.Verify(a => a.ChangedAsync(Project, "github-42", It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task OpenAsync(string thread, TicketBinding? ticket)
    {
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>().OpenAsync(
            Platform, "channel", thread, "user", new ActiveScope { Project = Project }, CancellationToken.None, ticket);
    }

    private async Task TurnAsync(string thread)
    {
        using var scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>().AppendTurnAsync(
            Platform, thread, TranscriptRole.User, "make it faster", kind: null, decision: null, CancellationToken.None);
    }
}
