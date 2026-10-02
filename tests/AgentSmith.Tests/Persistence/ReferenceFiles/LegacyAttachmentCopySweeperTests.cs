using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence.ReferenceFiles;

/// <summary>
/// 2026-10-01-283da: one housekeeping tick copies batch after batch until no legacy row is left
/// uncopied, through the registrations the server itself makes.
/// </summary>
public sealed class LegacyAttachmentCopySweeperTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public async Task LegacyAttachmentCopySweeper_SweepOnce_CopiesEveryBatchThenStops()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        for (var i = 0; i < 120; i++) await store.AddLegacyAsync("s-1", Png, DateTimeOffset.UtcNow);
        await using var provider = Services(store);
        var sweeper = provider.GetRequiredService<LegacyAttachmentCopySweeper>();

        (await sweeper.SweepOnceAsync(CancellationToken.None)).Should().Be(120, "three batches of at most fifty");
        (await sweeper.SweepOnceAsync(CancellationToken.None)).Should().Be(0, "nothing is left to copy");

        await using var read = store.Context();
        (await read.Set<ReferenceFile>().CountAsync()).Should().Be(120);
    }

    [Fact]
    public async Task LegacyAttachmentCopySweeper_Run_StopsWhenCancelled()
    {
        await using var store = await ReferenceFileStore.OpenAsync(ReferenceFileStore.Sqlite);
        await using var provider = Services(store);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await provider.GetRequiredService<LegacyAttachmentCopySweeper>()
            .Invoking(s => s.RunAsync(cancel.Token)).Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
    }

    private static ServiceProvider Services(ReferenceFileStore store)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddScoped(_ => store.Context());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>());
        services.AddSingleton(store.Translator);
        services.AddReferenceFiles();
        return services.BuildServiceProvider();
    }
}
