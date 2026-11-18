using System.Security.Cryptography;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
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
        await using var harness = Harness();
        var (state, changes) = await DraftAsync(harness);
        await using var scope = harness.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<DialogDeciderGrants>().Hold(mayWriteSecrets: true);

        var acceptance = await scope.ServiceProvider.GetRequiredService<IOutcomeSink>()
            .AcceptAsync(state, changes, mayStartRuns: true, CancellationToken.None);

        acceptance.Should().Be(OutcomeAcceptance.Acted);
        (await scope.ServiceProvider.GetRequiredService<ISecretVault>().StatesAsync(CancellationToken.None))
            .Should().NotContain(s => s.Name == "old_token");
    }

    // 2026-11-18-78efa: admin.assistant alone does not delete a stored value — the decider needs secrets.write.
    [Fact]
    public async Task AnOrphanedValue_AppliedByADeciderWithoutSecretsWrite_IsRefusedNamingIt_AndStays()
    {
        await using var harness = Harness();
        var (state, changes) = await DraftAsync(harness);
        await using var scope = harness.Services.CreateAsyncScope();

        var acceptance = await scope.ServiceProvider.GetRequiredService<IOutcomeSink>()
            .AcceptAsync(state, changes, mayStartRuns: true, CancellationToken.None);

        acceptance.Should().Be(OutcomeAcceptance.Refused);
        (await scope.ServiceProvider.GetRequiredService<ISecretVault>().StatesAsync(CancellationToken.None))
            .Should().Contain(s => s.Name == "old_token");
        var after = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .GetOpenByThreadAsync(state.Platform, state.ThreadId!, CancellationToken.None);
        after!.Transcript.Last().Text.Should().Contain("secrets.write").And.Contain("secret-values/old_token").And.NotContain(Value);
    }

    private static RealCompositionHarness Harness()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return AdminTurnRunnerTests.BuildHarness(services =>
        {
            services.RemoveAll<MasterKeyRing>();
            services.AddSingleton(new MasterKeyRing(name => name == MasterKeyRing.Variable ? key : null));
        });
    }

    private static async Task<(ConversationState State, ConfigChangeOutcome Changes)> DraftAsync(RealCompositionHarness harness)
    {
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
        return (state, changes);
    }
}
