using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Server.Services.Config;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0353: the Changes view crashed because the endpoint returned the raw ConfigChange
/// (no `fields` array) while the client dereferenced `fields.length`. These pin the DTO
/// the endpoint now returns: route-style kind, lowercase action, and a before/after
/// field diff that null-guards the create (no before) and delete (no after) sides.
/// </summary>
public sealed class ConfigChangeViewTests
{
    private static ConfigChange Change(
        ConfigEntityType type, ConfigChangeOperation op, string? before, string? after) =>
        new("c1", 1, DateTimeOffset.UtcNow, "operator", type, "sample-default", op, before, after, Reverted: false);

    [Fact]
    public void Update_DiffsOnlyChangedFields()
    {
        var view = ConfigChangeView.From(Change(
            ConfigEntityType.Agent, ConfigChangeOperation.Update,
            before: """{"type":"azure_openai","maxRunWallTimeSeconds":1800}""",
            after: """{"type":"azure_openai","maxRunWallTimeSeconds":5400}"""));

        view.EntityKind.Should().Be("agents");
        view.Action.Should().Be("update");
        view.Fields.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ConfigChangeFieldView("maxRunWallTimeSeconds", "1800", "5400"));
    }

    [Fact]
    public void Create_HasNoBeforeSide()
    {
        var view = ConfigChangeView.From(Change(
            ConfigEntityType.Tracker, ConfigChangeOperation.Create, before: null, after: """{"name":"sample-tracker"}"""));

        view.Action.Should().Be("create");
        view.Fields.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ConfigChangeFieldView("name", null, "sample-tracker"));
    }

    [Fact]
    public void Delete_HasNoAfterSide()
    {
        var view = ConfigChangeView.From(Change(
            ConfigEntityType.McpServer, ConfigChangeOperation.Delete, before: """{"name":"sample-tracker"}""", after: null));

        view.EntityKind.Should().Be("mcp-servers");
        view.Action.Should().Be("delete");
        view.Fields.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ConfigChangeFieldView("name", "sample-tracker", null));
    }

    [Fact]
    public void Settings_MapToTheSettingsKind_KeyedByTypeId()
    {
        var view = ConfigChangeView.From(new ConfigChange(
            "c2", 2, DateTimeOffset.UtcNow, "operator", ConfigEntityType.Settings, "orchestrator",
            ConfigChangeOperation.Update, BeforeJson: null, AfterJson: """{"maxRunWallTimeSeconds":5400}""",
            Reverted: false));

        view.EntityKind.Should().Be("settings");
        view.EntityId.Should().Be("orchestrator");
        view.Fields.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ConfigChangeFieldView("maxRunWallTimeSeconds", null, "5400"));
    }

    [Fact]
    public void MalformedOrEmptyBlobs_YieldEmptyDiff_NotACrash()
    {
        var view = ConfigChangeView.From(Change(
            ConfigEntityType.Connection, ConfigChangeOperation.Update, before: "not-json", after: null));

        view.EntityKind.Should().Be("connections");
        view.Fields.Should().BeEmpty();
    }
}
