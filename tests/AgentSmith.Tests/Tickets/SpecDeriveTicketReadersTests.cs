using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-26-bdd0b: a failed spec derivation carries the ticket id, and every reader of the ticket's work reads
/// past it — the bucket, the attempts, the previous attempt and the delivery window are what the code run left.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class SpecDeriveTicketReadersTests : IDisposable
{
    private const string Attempt = "2026-10-13T09-00-00-0001";
    private const string Derivation = "2026-10-13T10-00-00-0002";
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketReaders_FailedSpecDerive_LeavesBucketAttemptsAndDeliveryUntouched()
    {
        await _store.AddAsync(Run(Attempt, status: "success"), Step(Attempt));
        var before = (await ListAsync()).Tickets.Single();

        await _store.AddAsync(Run(Derivation, status: "failed", pipeline: PipelinePresets.SpecDeriveName), Step(Derivation));

        var after = (await ListAsync()).Tickets.Should().ContainSingle().Subject;
        after.Bucket.Should().Be(before.Bucket, "a failed derivation is not a failed attempt");
        after.AttemptCount.Should().Be(1);
        after.NewestRunId.Should().Be(Attempt);
        var detail = await _store.Detail.ReadAsync("alpha", "42", CancellationToken.None);
        detail!.Attempts.Select(a => a.RunId).Should().Equal(Attempt);
        (await Previous().LatestAsync("alpha", "42", null, CancellationToken.None))!.RunId.Should().Be(Attempt);
        (await DeliveryRunIdsAsync()).Should().Equal(Attempt);
    }

    private async Task<List<string>> DeliveryRunIdsAsync()
    {
        using var scope = _store.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<Run>().AsNoTracking()
            .InWindow(ProjectScope.Everything, "2026-10-13T00", "2026-10-14T00").Select(r => r.Id).ToListAsync();
    }

    private DbPreviousAttemptReader Previous() =>
        new(_store.Services.GetRequiredService<IServiceScopeFactory>(), new AgentSmith.Application.Services.Resume.NullDecidedDefaults());

    private Task<TicketListView> ListAsync() =>
        _store.List.ReadAsync(new TicketListRequest(ProjectScope.Everything, null, null, null), CancellationToken.None);
}
