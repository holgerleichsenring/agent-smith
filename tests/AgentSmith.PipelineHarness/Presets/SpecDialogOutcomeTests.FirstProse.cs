using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-09-0620n: a schema fix inside a turn contributes only its block. Live, the fix's one sentence replaced the
/// first reply's analysis and its open questions.
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    private const string FirstAnalysis =
        "Arbeitsbild: Rebuild, Serie.\n\nOffene Fragen:\n1. Wer darf exportieren?\n2. Wie lange bleiben Dateien liegen?";

    [Fact]
    public async Task Turn_InvalidThenValidBlock_KeepsTheFirstReplysProseAndQuestions()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient
            .EnqueueText($"{FirstAnalysis}\n\n```yaml\nspec: not-a-valid-spec-id\n```")
            .EnqueueText($"Fixed the block.\n{ValidDraft}");

        var result = await RunTurnAsync(harness, State("rebuild the export") with { Platform = "dashboard" });

        result.Outcome.Should().BeOfType<SpecOutcome>();
        result.Reply.Should().StartWith(FirstAnalysis).And.Contain("2. Wie lange bleiben Dateien liegen?")
            .And.Contain("Add a widget endpoint to the sample service").And.NotContain("Fixed the block.")
            .And.NotContain("not-a-valid-spec-id");
    }
}
