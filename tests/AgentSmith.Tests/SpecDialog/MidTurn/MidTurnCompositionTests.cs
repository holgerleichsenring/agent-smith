using AgentSmith.Application.Services.Turns;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog.MidTurn;

/// <summary>2026-11-12-a1fa: the server's own composition builds the router over one inbox per process, beside the
/// one gate, and hands the master the surface that delivers mid-turn messages.</summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class MidTurnCompositionTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _context;

    public MidTurnCompositionTests() => _context = MigratedStoreTemplate.Context(_connection);

    [Fact]
    public void SpecDialogRouter_ResolvesQueuedTurnsOverOneInboxPerProcess()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        services.AddScoped(_ => new SpecDialogSessionRepository(_context));
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        first.ServiceProvider.GetRequiredService<SpecDialogRouter>().Should().NotBeNull();
        first.ServiceProvider.GetRequiredService<SpecDialogQueuedTurns>().Should().NotBeNull();
        first.ServiceProvider.GetRequiredService<SpecDialogOperatorInbox>()
            .Should().BeSameAs(second.ServiceProvider.GetRequiredService<SpecDialogOperatorInbox>(), "one inbox per process");
        provider.GetRequiredService<MidTurnMessageTools>().Should().NotBeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
