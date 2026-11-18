using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-f3a8c: an admin asks the assistant to duplicate a tracker — the scripted master reads it and drafts the
/// copy, the turn proposes the change, and Apply runs a config run that writes it through the studio's store with a
/// change row naming the person and the run.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AdminConfigChangeTests
{
    [Fact]
    public async Task DuplicateATracker_DraftThenApply_ConfigRunWritesItAttributed()
    {
        await using var harness = AdminTurnRunnerTests.BuildHarness();
        var state = await AdminTurnRunnerTests.OpenAsync(harness, "duplicate the github tracker as github-copy");
        harness.ChatClient
            .EnqueueToolCall("read_config_entity", """{"kind": "trackers", "id": "github-fixture"}""")
            .EnqueueToolCall("draft_config_change", """{"operation": "upsert", "kind": "trackers", "id": "github-copy", "document": "{\"type\":\"github\",\"authSecret\":\"gh_token\",\"url\":\"https://github.com/agent-smith-fixtures/repo\"}"}""")
            .EnqueueText("Drafted github-copy; press Apply.");

        var turn = await AdminTurnRunnerTests.TurnAsync(harness, state);

        var changes = turn!.Outcome.Should().BeOfType<ConfigChangeOutcome>().Subject;
        changes.Changes.Should().ContainSingle(c => c.Id == "github-copy" && c.Before == null);

        await using var scope = harness.Services.CreateAsyncScope();
        var acceptance = await scope.ServiceProvider.GetRequiredService<IOutcomeSink>()
            .AcceptAsync(state, changes, mayStartRuns: true, CancellationToken.None);

        acceptance.Should().Be(OutcomeAcceptance.Acted);
        var store = scope.ServiceProvider.GetRequiredService<IConfigStore>();
        store.GetTrackers().Should().Contain(t => t.Id == "github-copy" && t.Type == "github");
        store.GetChanges().Should().Contain(c => c.EntityId == "github-copy"
            && c.Actor.StartsWith("U-admin via admin assistant (run ", StringComparison.Ordinal));
    }
}
