using System.Net.Http.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Startup;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// The rig's own claims. Every one of them is read off the composition or off the server's
/// answer — never off a stopwatch: three timing assertions in this suite went red under
/// load on the day this was written, and a rig that is only fast when the machine is idle
/// has not fixed anything.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class BootedServerRigTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public async Task Rig_ABootedCaseWithNoRedisAssertion_PerformsNoBlockingConnect()
    {
        await using var server = await BootedServer.StartAsync(NewConfig());

        var transport = server.Services.GetRequiredService<IConnectionMultiplexer>();
        transport.Should().NotBeOfType<ConnectionMultiplexer>(
            "a real multiplexer IS a socket, and the connect it performs while the hosted "
            + "services are constructed is the wait this rig exists to stop paying");
        transport.Configuration.Should().Be(InMemoryRedis.Endpoint);
        (await FindingsAsync(server)).Should().NotContain(f => f.Subsystem == "redis",
            "the startup probe pinged the substitute and it answered — an endpoint that "
            + "never answers is what spends the probe's whole budget");
    }

    [Fact]
    public async Task Rig_ABootedCase_StartsOnlyTheHostedServicesItNames()
    {
        await using var server = await BootedServer.StartAsync(
            new BootPlan(NewConfig()) { HostedServices = [typeof(RecordingHostedService)] });

        var hosted = server.Services.GetServices<IHostedService>().ToList();
        hosted.OfType<RecordingHostedService>().Should().ContainSingle()
            .Which.WasStarted.Should().BeTrue("a case gets the service it named, started");
        hosted.Should().NotContain(s => s.GetType().Assembly == typeof(ServerHostFactory).Assembly,
            "the reapers, pollers and pumps are not what a routing or an authorization "
            + "assertion is about, and the host constructs every one before it starts any");
    }

    [Fact]
    public async Task Rig_TwoBootedCases_ShareOneMigratedSchema()
    {
        var migrations = MigratedStoreTemplate.TimesMigrated;
        var first = NewConfig(NewMigratedDb());
        var second = NewConfig(NewMigratedDb());

        await using (var server = await BootedServer.StartAsync(first))
            (await FindingsAsync(server)).Should().NotContain(f => f.Subsystem == "database");
        await using (var server = await BootedServer.StartAsync(second))
            (await FindingsAsync(server)).Should().NotContain(f => f.Subsystem == "database");

        MigratedStoreTemplate.TimesMigrated.Should().Be(Math.Max(migrations, 1),
            "both stores carry the whole schema and the migration set ran at most once for "
            + "the process — a second run would mean the template stopped being shared");
    }

    [Fact]
    public void Rig_ACaseThatAssertsOnAnUnreachableDependency_StillPointsAtNothing()
    {
        var subject = new BootPlan("ignored") { UnreachableRedis = BootPlan.NothingAnswers };

        subject.RedisUrl.Should().Be(BootPlan.NothingAnswers);
        Composed(subject).Should().NotBeNull(
            "the case whose SUBJECT is the wait keeps the server's own registration, which "
            + "is the only thing that can still connect to nothing and report it");
        Composed(new BootPlan("ignored")).Should().BeNull(
            "and no other case inherits it: the default replaces that registration outright");
    }

    /// <summary>The server's own Redis registration, or null once it has been substituted.</summary>
    private static ServiceDescriptor? Composed(BootPlan plan)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(
            _ => throw new InvalidOperationException("a real connect"));
        BootSubstitutions.For(plan)(services);
        return services.SingleOrDefault(d =>
            d.ServiceType == typeof(IConnectionMultiplexer) && d.ImplementationFactory is not null);
    }

    private static async Task<IReadOnlyList<StartupFindingView>> FindingsAsync(BootedServer server) =>
        (await server.Client.GetFromJsonAsync<StartupFindingsResponse>("/api/config/findings"))!.Findings;

    private string NewMigratedDb()
    {
        var path = Temp("db");
        _tempFiles.Add(path + "-wal");
        _tempFiles.Add(path + "-shm");
        MigratedStoreTemplate.CopyToFile(path);
        return path;
    }

    private string NewConfig(string? dbPath = null)
    {
        var path = Temp("yml");
        File.WriteAllText(path, $"""
            persistence:
              provider: sqlite
              connection_string: Data Source={dbPath ?? Temp("db")}

            """);
        return path;
    }

    private string Temp(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-rig-{Guid.NewGuid():N}.{extension}");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
            if (File.Exists(file)) File.Delete(file);
    }
}
