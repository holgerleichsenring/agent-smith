using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-11-16-f3fbb: the previous attempt stopped on a precondition when it finished Failed with an end
/// kind in RunEndKinds.Preconditions — read off the Run row on a real SQLite engine, with its detail.</summary>
public sealed class PreviousAttemptPreconditionTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    public void Dispose() => _connection.Dispose();

    private AgentSmithDbContext Db() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private async Task<PreviousAttempt> LatestAsync(string status, string? kind, EndDetail? detail = null)
    {
        await using (var db = Db())
        {
            db.Add(new Run
            {
                Id = "2026-11-16T09-00-00-a", Project = "p", TicketId = "42", Pipeline = "code", Status = status,
                StartedAt = T0, FinishedAt = T0.AddMinutes(1), EndKind = kind, EndDetailJson = EndDetailJson.Write(detail),
            });
            await db.SaveChangesAsync();
        }
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        var scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return (await new DbPreviousAttemptReader(scopes, new DecidedDefaults(scopes))
            .LatestAsync("p", "42", null, CancellationToken.None))!;
    }

    [Theory]
    [InlineData(RunEndKinds.Preflight)]
    [InlineData(RunEndKinds.ImageUntrusted)]
    [InlineData(RunEndKinds.BootstrapMissing)]
    public async Task PreviousAttempt_FailedOnAPreconditionKind_StoppedOnPrecondition(string kind)
    {
        var detail = new EndDetail("Image node:22", [], "Trusted image registries", "docker.io/");

        var attempt = await LatestAsync(RunStatuses.Failed, kind, detail);

        attempt.StoppedOnPrecondition.Should().BeTrue();
        (attempt.EndKind, attempt.EndDetail!.Subject).Should().Be((kind, "Image node:22"));
    }

    [Theory]
    [InlineData(RunStatuses.Failed, RunEndKinds.ReadinessUnknown)]
    [InlineData(RunStatuses.Failed, RunEndKinds.CriteriaUnmet)]
    [InlineData(RunStatuses.Failed, null)]
    [InlineData(RunStatuses.Success, RunEndKinds.Preflight)]
    public async Task PreviousAttempt_OtherEnd_NotStoppedOnPrecondition(string status, string? kind) =>
        (await LatestAsync(status, kind)).StoppedOnPrecondition.Should().BeFalse();
}
