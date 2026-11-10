using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services.Prompts;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Prompts;

/// <summary>2026-11-09-0620a: a migrated SQLite store with the prompt recorder and reader over it.</summary>
public sealed class PromptStoreHarness : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _services;

    public PromptStoreHarness()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AgentSmithDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>());
        _services = services.BuildServiceProvider();
        Recorder = new PromptRecorder(
            _services.GetRequiredService<IServiceScopeFactory>(), new PromptRecentRecords(),
            NullLogger<PromptRecorder>.Instance);
    }

    public PromptRecorder Recorder { get; }

    public AgentSmithDbContext Db() => new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }
}
