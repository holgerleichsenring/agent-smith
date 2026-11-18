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
    private readonly Mock<ISecretVault> _vault = new();
    private readonly ConfigEntityKinds _kinds = new();
    private static readonly DateTimeOffset SetAt = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    public ConfigChangeApplierTests()
    {
        _store.Setup(s => s.GetTrackers()).Returns([]);
        _store.Setup(s => s.GetAgents()).Returns([]);
        _store.Setup(s => s.SettingTypes).Returns(["dialogue", "role_mapping"]);
        _store.Setup(s => s.GetChanges()).Returns([]);
        _store.Setup(s => s.GetSecrets()).Returns([]);
        _vault.Setup(v => v.StatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            [new StoredSecretState("old_token", "ada", SetAt, "env:1", true, true)]);
    }

    [Fact]
    public async Task Apply_AnUpsert_GoesThroughTheStudiosStoreCallUnderThePersonAndTheRun()
    {
        var report = await Applier().ApplyAsync(Request(Upsert("trackers", "jira-b", """{"type":"jira","authSecret":"jira"}""")), "run-1", CancellationToken.None);

        report.Applied.Should().BeTrue();
        _store.Verify(s => s.UpsertTracker(It.Is<TrackerEntity>(t => t.Id == "jira-b" && t.Type == "jira"),
            It.Is<ChangeAttribution>(a => a.Actor == "ada via admin assistant (run run-1)")));
    }

    [Fact]
    public async Task Apply_ASecondItemRefused_RevertsTheFirstAndNamesTheItem()
    {
        _store.Setup(s => s.UpsertAgent(It.IsAny<AgentEntity>(), It.IsAny<ChangeAttribution>()))
            .Throws(new ConfigurationException("agent 'x' names an unknown model"));
        _store.Setup(s => s.GetChanges()).Returns([new ConfigChange("c-1", 1, DateTimeOffset.UtcNow,
            "ada via admin assistant (run run-2)", ConfigEntityType.Tracker, "jira-b", ConfigChangeOperation.Create, null, "{}", false)]);

        var report = await Applier().ApplyAsync(Request(Upsert("trackers", "jira-b", """{"type":"jira"}"""), Upsert("agents", "x", """{"type":"openai"}""")), "run-2", CancellationToken.None);

        report.Applied.Should().BeFalse();
        report.Line.Should().Contain("Change 2 (upsert agents/x)").And.Contain("unknown model").And.Contain("were reverted");
        _store.Verify(s => s.Revert("c-1", It.IsAny<ChangeAttribution>()));
    }

    [Fact]
    public async Task Apply_AnEntityChangedSinceItWasDrafted_IsRefused()
    {
        _store.Setup(s => s.GetTrackers()).Returns([new TrackerEntity("jira-b", "jira", "other")]);

        var report = await Applier().ApplyAsync(Request(Upsert("trackers", "jira-b", """{"type":"jira"}""")), "run-3", CancellationToken.None);

        report.Line.Should().Contain("changed since it was drafted");
        _store.Verify(s => s.UpsertTracker(It.IsAny<TrackerEntity>(), It.IsAny<ChangeAttribution>()), Times.Never);
    }

    // 2026-11-18-00eaa: an orphaned stored value is deleted after the catalog items, and only once every such item
    // still matches its draft; what the draft and the model see is the name and when it was set, nothing else.
    [Fact]
    public async Task Apply_AnOrphanedValue_IsDeletedAfterTheCatalogItems()
    {
        var orphans = new OrphanedSecretValues(_vault.Object);
        var before = await orphans.DraftAsync(_store.Object, ConfigChangeItem.Delete, "old_token", CancellationToken.None);

        var report = await Applier().ApplyAsync(Request(
            new ConfigChangeItem(ConfigChangeItem.Delete, OrphanedSecretValues.Kind, "old_token", null, before),
            Upsert("trackers", "jira-b", """{"type":"jira"}""")), "run-4", CancellationToken.None);

        report.Applied.Should().BeTrue(report.Line);
        before.Should().Contain("old_token").And.NotContain("ada").And.NotContain("env:1");
        _store.Verify(s => s.UpsertTracker(It.IsAny<TrackerEntity>(), It.IsAny<ChangeAttribution>()));
        _vault.Verify(v => v.DeleteAsync("old_token", It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Apply_AValueWhoseSecretCameBack_IsRefusedBeforeAnyValueIsDeleted_AndTheCatalogReverted()
    {
        var before = await new OrphanedSecretValues(_vault.Object).DraftAsync(_store.Object, ConfigChangeItem.Delete, "old_token", CancellationToken.None);
        _store.Setup(s => s.GetSecrets()).Returns([new SecretEntity("OLD_TOKEN")]);
        _vault.Setup(v => v.StatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new StoredSecretState("old_token", "ada", SetAt, "env:1", true, true), new StoredSecretState("other", "ada", SetAt, "env:1", true, true)]);
        _store.Setup(s => s.GetChanges()).Returns([new ConfigChange("c-9", 1, DateTimeOffset.UtcNow,
            "ada via admin assistant (run run-5)", ConfigEntityType.Tracker, "jira-b", ConfigChangeOperation.Create, null, "{}", false)]);

        var report = await Applier().ApplyAsync(Request(Upsert("trackers", "jira-b", """{"type":"jira"}"""),
            new ConfigChangeItem(ConfigChangeItem.Delete, OrphanedSecretValues.Kind, "other", null, """{"name":"other","setAt":"2026-11-17T10:00:00+00:00"}"""),
            new ConfigChangeItem(ConfigChangeItem.Delete, OrphanedSecretValues.Kind, "old_token", null, before)), "run-5", CancellationToken.None);

        report.Applied.Should().BeFalse();
        report.Line.Should().Contain("Change 3 (delete secret-values/old_token)").And.Contain("were reverted");
        _vault.Verify(v => v.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.Revert("c-9", It.IsAny<ChangeAttribution>()));
    }

    [Fact]
    public async Task Draft_AStoredValue_OnlyItsDeletion_AndOnlyAnOrphan()
    {
        var orphans = new OrphanedSecretValues(_vault.Object);

        await FluentActions.Awaiting(() => orphans.DraftAsync(_store.Object, ConfigChangeItem.Upsert, "old_token", CancellationToken.None))
            .Should().ThrowAsync<ConfigurationException>().WithMessage("*never drafted*");
        _store.Setup(s => s.GetSecrets()).Returns([new SecretEntity("old_token")]);
        await FluentActions.Awaiting(() => orphans.DraftAsync(_store.Object, ConfigChangeItem.Delete, "old_token", CancellationToken.None))
            .Should().ThrowAsync<ConfigurationException>().WithMessage("*secret drawer*");
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

    private ConfigChangeApplier Applier() =>
        new(_store.Object, _kinds, new OrphanedSecretValues(_vault.Object), NullLogger<ConfigChangeApplier>.Instance);

    private static ConfigChangeRequest Request(params ConfigChangeItem[] items) => new(items, "ada", MayWriteSecrets: true);

    private static ConfigChangeItem Upsert(string kind, string id, string document) =>
        new(ConfigChangeItem.Upsert, kind, id, document, null);
}
