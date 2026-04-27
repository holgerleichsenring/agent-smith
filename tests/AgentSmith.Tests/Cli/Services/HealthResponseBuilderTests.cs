using AgentSmith.Application.Services.Health;
using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using System.Text.Json;

namespace AgentSmith.Tests.Cli.Services;

public sealed class HealthResponseBuilderTests
{
    [Fact]
    public void Liveness_AllUp_StatusIsOk()
    {
        var subsystems = new ISubsystemHealth[] { Up("webhook"), Up("queue_consumer"), Up("redis") };

        var (code, body) = HealthResponseBuilder.Liveness(subsystems);

        code.Should().Be(200);
        Field(body, "status").Should().Be("ok");
    }

    [Fact]
    public void Liveness_RedisDown_StatusIsDegradedHttp200()
    {
        var subsystems = new ISubsystemHealth[]
        {
            Up("webhook"),
            Degraded("queue_consumer", "waiting for Redis"),
            Degraded("redis", "connection refused")
        };

        var (code, body) = HealthResponseBuilder.Liveness(subsystems);

        code.Should().Be(200);
        Field(body, "status").Should().Be("degraded");
    }

    [Fact]
    public void Liveness_BodyListsSubsystems()
    {
        var subsystems = new ISubsystemHealth[] { Up("webhook"), Disabled("redis", "REDIS_URL not configured") };

        var (_, body) = HealthResponseBuilder.Liveness(subsystems);

        var doc = JsonDocument.Parse(body);
        var arr = doc.RootElement.GetProperty("subsystems");
        arr.GetArrayLength().Should().Be(2);
        arr[0].GetProperty("name").GetString().Should().Be("webhook");
        arr[0].GetProperty("state").GetString().Should().Be("up");
        arr[1].GetProperty("name").GetString().Should().Be("redis");
        arr[1].GetProperty("state").GetString().Should().Be("disabled");
        arr[1].GetProperty("reason").GetString().Should().Be("REDIS_URL not configured");
    }

    [Fact]
    public void Readiness_AllUp_Returns200()
    {
        var subsystems = new ISubsystemHealth[] { Up("webhook"), Up("queue_consumer"), Up("redis") };

        var (code, body) = HealthResponseBuilder.Readiness(subsystems);

        code.Should().Be(200);
        Field(body, "status").Should().Be("ready");
    }

    [Fact]
    public void Readiness_AnyDown_Returns503()
    {
        var subsystems = new ISubsystemHealth[] { Up("webhook"), Down("queue_consumer", "broken") };

        var (code, _) = HealthResponseBuilder.Readiness(subsystems);

        code.Should().Be(503);
    }

    [Fact]
    public void Readiness_AnyDegraded_Returns503()
    {
        var subsystems = new ISubsystemHealth[] { Up("webhook"), Degraded("redis", "connecting") };

        var (code, _) = HealthResponseBuilder.Readiness(subsystems);

        code.Should().Be(503);
    }

    [Fact]
    public void Readiness_AnyDisabled_Returns503()
    {
        var subsystems = new ISubsystemHealth[] { Up("webhook"), Disabled("redis", "REDIS_URL not configured") };

        var (code, _) = HealthResponseBuilder.Readiness(subsystems);

        code.Should().Be(503);
    }

    [Fact]
    public void Readiness_BodyListsNonUpSubsystemsWithReason()
    {
        var subsystems = new ISubsystemHealth[]
        {
            Up("webhook"),
            Disabled("queue_consumer", "REDIS_URL not configured"),
            Disabled("redis", "REDIS_URL not configured")
        };

        var (_, body) = HealthResponseBuilder.Readiness(subsystems);

        var doc = JsonDocument.Parse(body);
        var arr = doc.RootElement.GetProperty("subsystems");
        var nonUp = Enumerable.Range(0, arr.GetArrayLength())
            .Select(i => arr[i])
            .Where(e => e.GetProperty("state").GetString() != "up")
            .ToList();
        nonUp.Should().HaveCount(2);
        nonUp.Should().AllSatisfy(e =>
            e.GetProperty("reason").GetString().Should().Be("REDIS_URL not configured"));
    }

    private static SubsystemHealth Up(string name)
    {
        var h = new SubsystemHealth(name);
        h.SetUp();
        return h;
    }

    private static SubsystemHealth Down(string name, string reason)
    {
        var h = new SubsystemHealth(name);
        h.SetDown(reason);
        return h;
    }

    private static SubsystemHealth Degraded(string name, string reason)
    {
        var h = new SubsystemHealth(name);
        h.SetDegraded(reason);
        return h;
    }

    private static SubsystemHealth Disabled(string name, string reason)
    {
        var h = new SubsystemHealth(name);
        h.SetDisabled(reason);
        return h;
    }

    private static string Field(string json, string key)
        => JsonDocument.Parse(json).RootElement.GetProperty(key).GetString()!;
}
