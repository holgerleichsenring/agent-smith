using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.Browser;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.SpecDialog.Ratification;

/// <summary>
/// 2026-10-13-8762s: a real container over the shipped SQLite schema — every scope its own unit of work, as
/// every dashboard message runs in a scope of its own — with the ratification, the session manager and the
/// filing stamp resolved the way the server composes them.
/// </summary>
internal sealed class RatificationRig : IDisposable
{
    public const string Platform = "dashboard";
    public const string Thread = "d-8762s";
    public const string Owner = "person-a";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public RatificationRig()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options));
        services.AddScoped<SpecDialogSessionRepository>();
        services.AddSingleton(Activity.Object).AddSingleton(Sandbox.Holds.None()).AddSingleton(TimeProvider.System);
        services.AddSingleton(Mocks).AddSingleton<IConversationMockReader>(Mocks);
        services.AddSingleton<SpecDialogTurnGate>();
        services.AddScoped<SpecDialogSessionManager>().AddScoped<BoundSessionNudge>();
        services.AddScoped<SpecDialogOwnership>().AddScoped<MockRatification>();
        services.AddTransient<RatifiedMockStamp>();
        Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public ServiceProvider Provider { get; }

    public Mock<ITicketActivityNudge> Activity { get; } = new();

    public MemoryMocks Mocks { get; } = new();

    public SpecDialogTurnGate Gate => Provider.GetRequiredService<SpecDialogTurnGate>();

    public async Task<string> OpenAsync(TicketBinding? ticket = null)
    {
        using var scope = Provider.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>().OpenAsync(Platform, Thread, Thread,
            Owner, new ActiveScope { Project = "sample" }, CancellationToken.None, ticket)).JobId;
    }

    /// <summary>A ratification as a request of its own: a fresh scope, its own unit of work.</summary>
    public async Task<MockRatificationResult> RatifyAsync(string session, string specId, int version, string owner = Owner)
    {
        using var scope = Provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MockRatification>()
            .RatifyAsync(session, specId, version, owner, CancellationToken.None);
    }

    public static Task<ConversationState?> AppendAsync(IServiceScope scope, TranscriptRole role, string text,
        SpecDialogTurnKind? kind = null, SpecDialogDecision? decision = null) =>
        scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .AppendTurnAsync(Platform, Thread, role, text, kind, decision, CancellationToken.None);

    /// <summary>The transcript as a fresh scope reads it from the store.</summary>
    public async Task<IReadOnlyList<TranscriptTurn>> TranscriptAsync()
    {
        using var scope = Provider.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .GetOpenByThreadAsync(Platform, Thread, CancellationToken.None))!.Transcript;
    }

    public void Dispose()
    {
        Provider.Dispose();
        _connection.Dispose();
    }
}
