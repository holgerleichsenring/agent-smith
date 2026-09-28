using System.Text.Json;
using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

public sealed class SubsystemHealthSectionTests
{
    [Fact]
    public void SubsystemHealthSection_EverySubsystem_ListedWithStateAndReason()
    {
        var up = new SubsystemHealth("queue_consumer");
        up.SetUp();
        var degraded = new SubsystemHealth("redis");
        degraded.SetDegraded("SocketFailure: connection refused");

        var entries = Serialize(SubsystemHealthSection.From([up, degraded]));

        entries.Should().HaveCount(2);
        entries[0].GetProperty("name").GetString().Should().Be("queue_consumer");
        entries[0].GetProperty("state").GetString().Should().Be("up");
        entries[1].GetProperty("state").GetString().Should().Be("degraded");
        entries[1].GetProperty("reason").GetString().Should().Be("SocketFailure: connection refused");
        entries[1].GetProperty("last_changed_utc").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SubsystemHealthSection_NoneRegistered_EmptyArray()
    {
        SubsystemHealthSection.From([]).Should().BeEmpty();
        SubsystemHealthSection.Status([]).Should().Be("ok");
    }

    [Fact]
    public void Status_DisabledSubsystem_StaysOk_DownSubsystem_Degrades()
    {
        var disabled = new SubsystemHealth("poller");
        disabled.SetDisabled("no pollers configured");
        var down = new SubsystemHealth("housekeeping");
        down.SetDown("lease lost");

        SubsystemHealthSection.Status([disabled]).Should().Be("ok");
        SubsystemHealthSection.Status([disabled, down]).Should().Be("degraded");
    }

    private static JsonElement[] Serialize(object[] section) =>
        JsonDocument.Parse(JsonSerializer.Serialize(section)).RootElement.EnumerateArray().ToArray();
}
