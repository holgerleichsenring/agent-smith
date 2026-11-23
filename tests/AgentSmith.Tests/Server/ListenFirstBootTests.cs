using System.Net;
using AgentSmith.Server.Services.Hosting;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-11-22-747db: the REAL composition with every worker the product registers, and a Redis multiplexer that
/// takes seconds to build — the synchronous connect that held the port shut for 5.8 s with Redis refused and 15.8 s
/// blackholed. The server must be listening long before it.
/// </summary>
[Trait("TestProcess", "env-3")]
[Collection(EnvVarCollection.Name)]
public sealed class ListenFirstBootTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("listen-first-").FullName;

    [Fact]
    public async Task Boot_EveryWorkerAndASlowMultiplexer_ListensBeforeAnyOfThem()
    {
        var registered = new List<ServiceDescriptor>();
        using var connect = new ManualResetEventSlim();
        var resolvedBeforeListening = false;
        await using var server = await BootedServer.StartAsync(new BootPlan(Config())
        {
            ProductWorkers = true,
            Services = services =>
            {
                services.RemoveAll<IConnectionMultiplexer>();
                // A connect that does not finish until this case lets it: a startup path that resolved it would
                // never get the port bound.
                services.AddSingleton(sp =>
                {
                    resolvedBeforeListening |= !sp.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested;
                    connect.Wait(TestWaits.Hang);
                    return InMemoryRedis.Connection();
                });
                registered.AddRange(services.Where(d => d.ServiceType == typeof(IHostedService)));
            },
        });
        var live = await server.Client.GetAsync("/health/live");
        connect.Set();

        resolvedBeforeListening.Should().BeFalse("nothing on the way to the listener builds the multiplexer");
        live.StatusCode.Should().Be(HttpStatusCode.OK, "the port is bound while the connect still hangs");
        registered.Should().NotBeEmpty();
        registered.Where(IsProduct).Should().OnlyContain(d => IsLauncher(d),
            "every product worker reaches the host through AddWorker, so it starts after the listener");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private string Config()
    {
        var db = Path.Combine(_dir, "boot.db");
        MigratedStoreTemplate.CopyToFile(db);
        var path = Path.Combine(_dir, "agentsmith.yml");
        File.WriteAllText(path, $"persistence:\n  provider: sqlite\n  connection_string: Data Source={db}\n");
        return path;
    }

    private static bool IsProduct(ServiceDescriptor d) =>
        (d.ImplementationType ?? d.ImplementationFactory?.Method.DeclaringType)?.Assembly.GetName().Name?
            .StartsWith("AgentSmith", StringComparison.Ordinal) == true;

    private static bool IsLauncher(ServiceDescriptor d) =>
        d.ImplementationType is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(WorkerLauncher<>);
}
