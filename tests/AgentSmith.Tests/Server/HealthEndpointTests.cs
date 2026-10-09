using System.Net;
using System.Text.Json;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// /health is the liveness probe: it answers 200 whatever the subsystems say, and the body
/// names each subsystem's state so an operator curling it sees which one is not up and why.
/// Boots the real composition with Redis pointed at nothing — the degradation under test.
/// </summary>
[Trait("TestProcess", "env-3")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class HealthEndpointTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public async Task HealthEndpoint_RedisDegraded_Still200_BodyNamesRedisDegraded()
    {
        var dbPath = Temp(".db");
        var configPath = Temp(".yml");
        File.WriteAllText(configPath, $"""
            persistence:
              provider: sqlite
              connection_string: Data Source={dbPath}

            """);
        await using var server = await BootedServer.StartAsync(
            new BootPlan(configPath) { UnreachableRedis = BootPlan.NothingAnswers });

        var response = await server.Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "liveness must not pull a degraded pod");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("degraded");
        var redis = body.RootElement.GetProperty("subsystems").EnumerateArray()
            .Single(s => s.GetProperty("name").GetString() == "redis");
        redis.GetProperty("state").GetString().Should().Be("degraded");
        redis.GetProperty("reason").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private string Temp(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-health-{Guid.NewGuid():N}{extension}");
        _tempFiles.AddRange([path, path + "-wal", path + "-shm"]);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
            if (File.Exists(file)) File.Delete(file);
    }
}
