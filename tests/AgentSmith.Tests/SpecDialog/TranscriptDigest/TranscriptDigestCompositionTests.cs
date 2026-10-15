using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog.TranscriptDigest;

/// <summary>2026-10-14-dbceo: the server's own composition builds the router over the keeper and the digest writer.</summary>
[Trait("TestProcess", "env-1")]
[Collection(EnvVarCollection.Name)]
public sealed class TranscriptDigestCompositionTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AgentSmithDbContext _context;

    public TranscriptDigestCompositionTests()
    {
        _connection.Open();
        _context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    [Fact]
    public void SpecDialogRouter_ResolvesKeeperAndWriterFromTheRealServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
        services.AddScoped(_ => new SpecDialogSessionRepository(_context));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<SpecDialogRouter>().Should().NotBeNull();
        var keeper = scope.ServiceProvider.GetRequiredService<SpecDialogTurnKeeper>();
        keeper.Should().BeSameAs(scope.ServiceProvider.GetRequiredService<SpecDialogTurnKeeper>(), "the keeper is scoped");
        scope.ServiceProvider.GetRequiredService<SpecDialogDigestWriter>().Should().NotBeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
