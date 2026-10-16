using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Rework;

/// <summary>A hand-back's waiting row, answered on the pull request, ends as cancelled through the terminal writer.</summary>
public sealed class WaitingRunCloserTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public WaitingRunCloserTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        MigratedStoreTemplate.CopyInto(ctx);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task CloseSupersededAsync_WaitingRow_EndsCancelled()
    {
        using (var ctx = new AgentSmithDbContext(Options()))
        {
            ctx.Runs.Add(new Run
            {
                Id = "run-1", Project = "p", Pipeline = "code", TicketId = "42", Platform = "github",
                Status = "waiting_for_input", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            });
            await ctx.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton<QueuedRunProjection>();
        services.AddSingleton<RunFinalizationProjection>();

        await new WaitingRunCloser(new CancelTerminalWriter(services.BuildServiceProvider()), TimeProvider.System)
            .CloseSupersededAsync("run-1", CancellationToken.None);

        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == "run-1");
        run.Status.Should().Be("cancelled");
        run.FinishedAt.Should().NotBeNull();
        run.EndKind.Should().Be(AgentSmith.Contracts.Runs.RunEndKinds.Superseded); // 2026-10-16-86e6b
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}
