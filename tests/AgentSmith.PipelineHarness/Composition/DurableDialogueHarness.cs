using AgentSmith.Application.Services;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Presets;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0327/p0328: the durable-dialogue composition — a RealCompositionHarness
/// over a SHARED SQLite file (the state that survives the "restart"), with the
/// production durable-inbox transport, a synchronous event→DB projection and a
/// recorded job queue. Also builds the REAL CapacityQueuePump wired to a
/// ResumeRunLauncher so a matched checkpoint launches exactly like production.
/// </summary>
public static class DurableDialogueHarness
{
    public static RealCompositionHarness Build(
        string fixtureName, string dbPath, RecordingJobQueue jobQueue) =>
        RealCompositionHarness.Build(
            FixturePaths.For(fixtureName), SandboxBackend.Stub, session: null,
            SkillsBackend.Fixture, services =>
            {
                // Shared SQLite FILE: the durable state that survives the "restart".
                services.RemoveAll<DbContextOptions<AgentSmithDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<AgentSmithDbContext>();
                services.AddDbContext<AgentSmithDbContext>(b => b.UseSqlite($"Data Source={dbPath}"));
                // Fast tier has no Redis: project events synchronously into the DB
                // (production: RedisEventPublisher → RunDbProjector, same applier).
                services.RemoveAll<AgentSmith.Contracts.Events.IEventPublisher>();
                services.AddSingleton<AgentSmith.Contracts.Events.IEventPublisher>(sp =>
                    new ProjectingEventPublisher(sp.GetRequiredService<IServiceScopeFactory>()));
                // The use case resolves the skills catalog — network boundary, stubbed.
                services.RemoveAll<ISkillsCatalogResolver>();
                services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
                // The production server registration: durable inbox first, hot
                // stream second. The hot stream is irrelevant here (no live wait
                // across the restart), so the inner transport is a mock.
                services.RemoveAll<IDialogueTransport>();
                services.AddSingleton<IDialogueTransport>(sp =>
                    new Server.Services.Dialogue.DurableDialogueTransport(
                        Mock.Of<IDialogueTransport>(),
                        sp.GetRequiredService<IDialogueAnswerInbox>()));
                // The Redis job queue is the launch channel — recorded + JSON
                // round-tripped so the resume payload takes the production shape.
                services.RemoveAll<IRedisJobQueue>();
                services.AddSingleton<IRedisJobQueue>(jobQueue);
            });

    public static async Task MigrateAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>().Database.MigrateAsync();
    }

    public static async Task<CommandResult> ExecuteAsync(
        RealCompositionHarness harness, string fixtureName, PipelineRequest request)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ExecutePipelineUseCase>()
            .ExecuteAsync(request, FixturePaths.For(fixtureName), CancellationToken.None);
    }

    public static CapacityQueuePump BuildPump(
        RealCompositionHarness harness, string fixtureName, RecordingJobQueue jobQueue)
    {
        var sp = harness.Services;
        return new CapacityQueuePump(
            sp.GetRequiredService<ICapacityQueue>(),
            sp.GetRequiredService<ITicketClaimService>(),
            sp.GetRequiredService<AgentSmith.Contracts.Providers.ITicketProviderFactory>(),
            sp.GetRequiredService<AgentSmith.Contracts.Sandbox.ICapacityBudget>(),
            sp.GetRequiredService<AgentSmith.Contracts.Events.IEventPublisher>(),
            sp.GetRequiredService<IRunCancelStateReader>(),
            new ResumeRunLauncher(
                sp, sp.GetRequiredService<IActiveRunLease>(), jobQueue,
                sp.GetRequiredService<ICapacityQueue>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ResumeRunLauncher>>()),
            sp.GetRequiredService<IConfigurationLoader>(),
            FixturePaths.For(fixtureName),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CapacityQueuePump>>());
    }
}
