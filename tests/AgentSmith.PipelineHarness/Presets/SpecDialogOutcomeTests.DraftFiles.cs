using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-15-06fc: through the real composition, the design master writes its draft as files, revises one in place,
/// offers it, and the turn proposes the parse of those files with the offer standing.
/// </summary>
public sealed partial class SpecDialogOutcomeTests
{
    [Fact]
    public async Task DesignTurn_DraftFilesEditedInPlaceAndOffered_ProposesThemWithTheOffer()
    {
        await using var harness = BuildHarness(new InMemoryDialogueBridge(), new RecordingChatAdapter());
        harness.ChatClient.EnqueueEpicWrite(EpicParentYaml, EpicStorageYaml, EpicApiYaml)
            .EnqueueToolCall("edit_file", JsonSerializer.Serialize(new
            {
                path = "draft:specs/p9000b.yaml", old_string = "Add the widget endpoint", new_string = "Add the widget endpoint and its route",
            }))
            .EnqueueToolCall("offer_filing", JsonSerializer.Serialize(new { line = "Files the widget platform as two specs." }))
            .EnqueueText("The epic is ready to file.").EnqueueText("The epic is ready to file.");

        var result = await RunTurnAsync(harness, Discussed("plan the widget platform"));

        ToolAnswers(harness).Should().HaveCount(5).And.OnlyContain(answer => answer.StartsWith("Stored") || answer.StartsWith("Offered"));
        var epic = result.Outcome.Should().BeOfType<EpicOutcome>().Subject;
        epic.Children.Select(c => c.SpecId).Should().Equal("p9000a", "p9000b");
        epic.Children[1].Yaml.Should().Contain("Add the widget endpoint and its route");
        epic.FilingOffer.Should().Be("Files the widget platform as two specs.");
    }

    private static IReadOnlyList<string> ToolAnswers(RealCompositionHarness harness) =>
        [.. harness.ChatClient.CallMessages.Last().SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Select(r => r.Result?.ToString() ?? string.Empty)];
}
