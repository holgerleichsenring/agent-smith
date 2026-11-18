using System.Security.Cryptography;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-18-00eaa: a stored value whose catalog secret is gone — the scripted master reads it as kind secret-values
/// and drafts its deletion; Apply runs a config run and the value is gone. The model never sees a value.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AdminOrphanCleanupTests
{
    private const string Value = "orphan-value-0123456789";

    [Fact]
    public async Task AnOrphanedValue_DraftedThenApplied_IsDeletedByTheConfigRun()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var harness = AdminTurnRunnerTests.BuildHarness(services =>
        {
            services.RemoveAll<MasterKeyRing>();
            services.AddSingleton(new MasterKeyRing(name => name == MasterKeyRing.Variable ? key : null));
        });
        await using (var seed = harness.Services.CreateAsyncScope())
            await seed.ServiceProvider.GetRequiredService<ISecretVault>().SetAsync("old_token", new SecretInput(Value), "ada", CancellationToken.None);
        var state = await AdminTurnRunnerTests.OpenAsync(harness, "clean up stored values nothing uses");
        harness.ChatClient
            .EnqueueToolCall("read_config_entity", """{"kind": "secret-values", "id": "old_token"}""")
            .EnqueueToolCall("draft_config_change", """{"operation": "delete", "kind": "secret-values", "id": "old_token"}""")
            .EnqueueText("Drafted the removal of old_token; press Apply.");

        var turn = await AdminTurnRunnerTests.TurnAsync(harness, state);

        var changes = turn!.Outcome.Should().BeOfType<ConfigChangeOutcome>().Subject;
        changes.Changes.Should().ContainSingle(c => c.Kind == "secret-values" && c.Id == "old_token" && c.Before != null);
        harness.ChatClient.CallMessages.SelectMany(m => m).Should().NotContain(m => m.Text.Contains(Value));
        await using var scope = harness.Services.CreateAsyncScope();
        var acceptance = await scope.ServiceProvider.GetRequiredService<IOutcomeSink>()
            .AcceptAsync(state, changes, mayStartRuns: true, CancellationToken.None);

        acceptance.Should().Be(OutcomeAcceptance.Acted);
        (await scope.ServiceProvider.GetRequiredService<ISecretVault>().StatesAsync(CancellationToken.None))
            .Should().NotContain(s => s.Name == "old_token");
    }
}
