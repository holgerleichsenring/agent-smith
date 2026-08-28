using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using AgentSmith.Server.Services.Archive;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server.Archive;

/// <summary>
/// 2026-08-28-3793: the server's own rule, judged on a store rather than through a request
/// — what it refuses, what it tolerates, and what it clears so the tolerated rows can be
/// written over.
/// </summary>
public sealed class ServerRestorePolicyTests : IDisposable
{
    private readonly SqliteConnection _store = MigratedStoreTemplate.OpenCopy();

    [Fact]
    public async Task Policy_AnInstallationThatHasRunNothing_Passes()
    {
        await using var db = MigratedStoreTemplate.Context(_store);

        var act = () => Policy().EnforceAsync(db, Tables(db), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Policy_ARecordedRun_RefusesAndCountsThem()
    {
        await using var db = MigratedStoreTemplate.Context(_store);
        db.Runs.Add(new Run { Id = "one", Project = "p", Pipeline = "x", TicketId = "T" });
        db.Runs.Add(new Run { Id = "two", Project = "p", Pipeline = "x", TicketId = "T" });
        await db.SaveChangesAsync();

        var act = () => Policy().EnforceAsync(db, Tables(db), CancellationToken.None);

        (await act.Should().ThrowAsync<DataArchiveException>()).Which.Message
            .Should().Contain("already recorded 2 run(s)");
    }

    [Fact]
    public async Task Policy_TheServersOwnBookkeeping_IsClearedRatherThanRefused()
    {
        await using var db = MigratedStoreTemplate.Context(_store);
        db.ObservedCallers.Add(Caller());
        db.ConfigEntities.Add(RoleMapping());
        await db.SaveChangesAsync();

        await Policy().EnforceAsync(db, Tables(db), CancellationToken.None);

        (await db.ObservedCallers.CountAsync()).Should().Be(0);
        (await db.ConfigEntities.CountAsync()).Should().Be(0,
            "the archive carries the same tables with keys of its own, so tolerating the "
            + "rows is not enough — an insert onto an occupied key fails on the constraint");
    }

    [Fact]
    public async Task Policy_ARunThatWasRecorded_LeavesTheBookkeepingAlone()
    {
        await using var db = MigratedStoreTemplate.Context(_store);
        db.Runs.Add(new Run { Id = "one", Project = "p", Pipeline = "x", TicketId = "T" });
        db.ObservedCallers.Add(Caller());
        await db.SaveChangesAsync();

        var act = () => Policy().EnforceAsync(db, Tables(db), CancellationToken.None);

        await act.Should().ThrowAsync<DataArchiveException>();
        (await db.ObservedCallers.CountAsync()).Should().Be(1, "a refusal changes nothing");
    }

    private static ServerRestorePolicy Policy() => new(
        new NoRecordedRunCheck(),
        new ServerBookkeepingReset(NullLogger<ServerBookkeepingReset>.Instance));

    private static IReadOnlyList<IEntityType> Tables(AgentSmithDbContext db) =>
        new ArchiveTableOrder().Of(db.Model);

    private static ObservedCallerEntity Caller() => new()
    {
        Subject = "the-operator", NameClaim = "name", NameValue = "The Operator",
        RoleValues = "[]", GroupValues = "[]",
        FirstSeen = DateTimeOffset.UtcNow, LastSeen = DateTimeOffset.UtcNow,
    };

    private static ConfigEntity RoleMapping() => new()
    {
        EntityType = ConfigDocTypes.RoleMapping, EntityId = ConfigDocTypes.SingletonId,
        Doc = "{}", Version = 1, UpdatedBy = "bootstrap-migration",
    };

    public void Dispose() => _store.Dispose();
}
