using AgentSmith.Application.Services.Admin;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Admin;

/// <summary>
/// 2026-11-18-78efa: deleting a stored secret value needs secrets.write besides admin.assistant — the config run
/// refuses the whole draft up front without it, so no catalog item is applied and no value deleted.
/// </summary>
public sealed class SecretValuesApprovalTests
{
    private readonly Mock<IConfigStore> _store = new();
    private readonly Mock<ISecretVault> _vault = new();
    private const string Before = """{"name":"old_token","setAt":"2026-11-17T10:00:00+00:00"}""";

    public SecretValuesApprovalTests()
    {
        _store.Setup(s => s.GetTrackers()).Returns([]);
        _store.Setup(s => s.GetSecrets()).Returns([]);
        _store.Setup(s => s.GetChanges()).Returns([]);
    }

    [Fact]
    public async Task Apply_AStoredValueDeletion_WithoutSecretsWrite_IsRefusedNamingThePermission_AndNothingApplied()
    {
        var report = await Applier().ApplyAsync(new ConfigChangeRequest(
            [Upsert(), OrphanDelete()], "ada", MayWriteSecrets: false), "run-1", CancellationToken.None);

        report.Applied.Should().BeFalse();
        report.Line.Should().Contain("secrets.write").And.Contain("delete secret-values/old_token").And.Contain("Nothing was applied");
        _store.Verify(s => s.UpsertTracker(It.IsAny<TrackerEntity>(), It.IsAny<ChangeAttribution>()), Times.Never);
        _vault.Verify(v => v.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Apply_OnlyCatalogItems_WithoutSecretsWrite_IsApplied()
    {
        var report = await Applier().ApplyAsync(new ConfigChangeRequest([Upsert()], "ada", MayWriteSecrets: false), "run-2", CancellationToken.None);

        report.Applied.Should().BeTrue(report.Line);
    }

    [Fact]
    public void Refusal_IsNullWithTheGrant_OrWithoutAnyStoredValueItem()
    {
        SecretValuesApproval.Refusal([OrphanDelete()], mayWriteSecrets: true).Should().BeNull();
        SecretValuesApproval.Refusal([Upsert()], mayWriteSecrets: false).Should().BeNull();
        SecretValuesApproval.Refusal([OrphanDelete()], mayWriteSecrets: false).Should().Contain(SecretValuesApproval.Permission);
    }

    private ConfigChangeApplier Applier() => new(
        _store.Object, new ConfigEntityKinds(), new OrphanedSecretValues(_vault.Object), NullLogger<ConfigChangeApplier>.Instance);

    private static ConfigChangeItem Upsert() => new(ConfigChangeItem.Upsert, "trackers", "jira-b", """{"type":"jira"}""", null);

    private static ConfigChangeItem OrphanDelete() =>
        new(ConfigChangeItem.Delete, OrphanedSecretValues.Kind, "old_token", null, Before);
}
