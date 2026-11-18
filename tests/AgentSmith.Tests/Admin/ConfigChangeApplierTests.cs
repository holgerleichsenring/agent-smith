using AgentSmith.Application.Services.Admin;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Admin;

/// <summary>2026-11-17-f3a8c: approved changes go through the studio's store calls, in order, reverting on refusal.</summary>
public sealed class ConfigChangeApplierTests
{
    private readonly Mock<IConfigStore> _store = new();
    private readonly ConfigEntityKinds _kinds = new();

    public ConfigChangeApplierTests()
    {
        _store.Setup(s => s.GetTrackers()).Returns([]);
        _store.Setup(s => s.GetAgents()).Returns([]);
        _store.Setup(s => s.SettingTypes).Returns(["dialogue", "role_mapping"]);
        _store.Setup(s => s.GetChanges()).Returns([]);
    }

    [Fact]
    public void Apply_AnUpsert_GoesThroughTheStudiosStoreCallUnderThePersonAndTheRun()
    {
        var report = Applier().Apply(Request(Upsert("trackers", "jira-b", """{"type":"jira","authSecret":"jira"}""")), "run-1");

        report.Applied.Should().BeTrue();
        _store.Verify(s => s.UpsertTracker(It.Is<TrackerEntity>(t => t.Id == "jira-b" && t.Type == "jira"),
            It.Is<ChangeAttribution>(a => a.Actor == "ada via admin assistant (run run-1)")));
    }

    [Fact]
    public void Apply_ASecondItemRefused_RevertsTheFirstAndNamesTheItem()
    {
        _store.Setup(s => s.UpsertAgent(It.IsAny<AgentEntity>(), It.IsAny<ChangeAttribution>()))
            .Throws(new ConfigurationException("agent 'x' names an unknown model"));
        _store.Setup(s => s.GetChanges()).Returns([new ConfigChange("c-1", 1, DateTimeOffset.UtcNow,
            "ada via admin assistant (run run-2)", ConfigEntityType.Tracker, "jira-b", ConfigChangeOperation.Create, null, "{}", false)]);

        var report = Applier().Apply(Request(Upsert("trackers", "jira-b", """{"type":"jira"}"""), Upsert("agents", "x", """{"type":"openai"}""")), "run-2");

        report.Applied.Should().BeFalse();
        report.Line.Should().Contain("Change 2 (upsert agents/x)").And.Contain("unknown model").And.Contain("were reverted");
        _store.Verify(s => s.Revert("c-1", It.IsAny<ChangeAttribution>()));
    }

    [Fact]
    public void Apply_AnEntityChangedSinceItWasDrafted_IsRefused()
    {
        _store.Setup(s => s.GetTrackers()).Returns([new TrackerEntity("jira-b", "jira", "other")]);

        var report = Applier().Apply(Request(Upsert("trackers", "jira-b", """{"type":"jira"}""")), "run-3");

        report.Line.Should().Contain("changed since it was drafted");
        _store.Verify(s => s.UpsertTracker(It.IsAny<TrackerEntity>(), It.IsAny<ChangeAttribution>()), Times.Never);
    }

    [Fact]
    public void Kinds_TheRoleMapping_IsRefusedAndNotOffered()
    {
        var act = () => _kinds.Upsert(_store.Object, "settings/role_mapping", "", "{}", ChangeAttribution.System);

        act.Should().Throw<ConfigurationException>().WithMessage("*access page*");
        ConfigEntityKinds.Names(_store.Object).Should().Contain("settings/dialogue").And.NotContain("settings/role_mapping");
    }

    [Fact]
    public void Draft_TheSameEntityAgain_ReplacesItsChange()
    {
        var draft = new AdminConfigDraft();
        draft.Put(Upsert("trackers", "a", "{}"));
        draft.Put(Upsert("trackers", "a", """{"type":"jira"}"""));

        draft.Items.Should().ContainSingle().Which.Document.Should().Contain("jira");
        draft.Drop("trackers", "A").Should().BeTrue();
    }

    private ConfigChangeApplier Applier() => new(_store.Object, _kinds, NullLogger<ConfigChangeApplier>.Instance);

    private static ConfigChangeRequest Request(params ConfigChangeItem[] items) => new(items, "ada");

    private static ConfigChangeItem Upsert(string kind, string id, string document) =>
        new(ConfigChangeItem.Upsert, kind, id, document, null);
}
