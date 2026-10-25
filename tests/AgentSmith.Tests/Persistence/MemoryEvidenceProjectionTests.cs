using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-25-9324b: the memory-use event through the real applier — a spec's relied list replaces the one before
/// it, and a dispute appends one contradiction worth -2 with its reason, once per trail position.
/// </summary>
public sealed class MemoryEvidenceProjectionTests : IDisposable
{
    private const string RunId = "2026-10-25T1000-memory";
    private const string Spec = "2026-10-25-0001a";
    private static readonly DateTimeOffset T = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly MemoryCitation X = new("api", "github.com/o/api", "x");
    private static readonly MemoryCitation Y = new("api", "github.com/o/api", "y");

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;

    public MemoryEvidenceProjectionTests()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(Mock.Of<ITicketActivityNudge>());
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private static MemoryUseRecordedEvent Relied(params MemoryCitation[] relied) =>
        new(RunId, T, Spec, "sample", "A-1", relied, null, null);

    [Fact]
    public async Task HandForward_SecondCall_ReplacesReliedRows()
    {
        await ApplyAsync(Relied(X, Y), seq: 3);
        await ApplyAsync(Relied(X), seq: 5);

        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        (row.RepoKey, row.Slug, row.Kind, row.Weight, row.RunId, row.SpecId, row.Project, row.TicketId, row.EventSeq)
            .Should().Be(("github.com/o/api", "x", MemoryEvidenceKinds.Relied, 0d, RunId, Spec, "sample", "A-1", 5L));
    }

    [Fact]
    public async Task DisputeMemory_AppendsContradictedWithReason()
    {
        var dispute = new MemoryUseRecordedEvent(RunId, T, null, "sample", "A-1", [], X, "it led the spec wrong");

        await ApplyAsync(dispute, seq: 7);
        await ApplyAsync(dispute, seq: 7);

        var row = (await RowsAsync()).Should().ContainSingle("a replayed position is written once").Subject;
        (row.Slug, row.Kind, row.Weight, row.Reason, row.SpecId, row.At)
            .Should().Be(("x", MemoryEvidenceKinds.Contradicted, MemoryStandingWeights.Disputed, "it led the spec wrong", "", T));
    }

    [Fact]
    public void EventTypeResolver_ResolvesMemoryUseRecorded()
    {
        ((int)EventType.MemoryUseRecorded).Should().Be(89);
        EventTypeResolver.Resolve(EventType.MemoryUseRecorded).Should().Be(typeof(MemoryUseRecordedEvent));
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task ApplyAsync(MemoryUseRecordedEvent ev, long seq)
    {
        using var scope = _provider.CreateScope();
        await _provider.GetRequiredService<RunEventApplier>().ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, seq, CancellationToken.None);
    }

    private async Task<List<MemoryEvidence>> RowsAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<MemoryEvidence>().Where(r => r.RunId == RunId).ToListAsync();
    }
}
