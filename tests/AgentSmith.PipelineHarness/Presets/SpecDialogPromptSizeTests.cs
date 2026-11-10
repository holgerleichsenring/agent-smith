using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-10-14a3h: what a first design turn on an upload costs before the model has read anything — the system
/// prompt, the user turn and the tool list (names, descriptions, schemas) — measured through the real composition and
/// the embedded catalog, so a change to either is seen as a size.
/// </summary>
public sealed partial class SpecDialogWorkPictureTests
{
    private readonly ITestOutputHelper? _output;

    public SpecDialogWorkPictureTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ReferenceTurn_FirstCall_PromptAndToolsStayWithinTheirSize()
    {
        await using var harness = BuildRealCatalogHarness();
        var state = await OpenAsync(harness);
        await UploadAsync(harness, state.JobId);
        harness.ChatClient.EnqueueText("Rebuild, series.");

        await TurnAsync(harness, state);

        var messages = harness.ChatClient.CallMessages[0];
        var system = messages.Where(m => m.Role == ChatRole.System).Sum(m => m.Text?.Length ?? 0);
        var user = messages.Where(m => m.Role == ChatRole.User).Sum(m => m.Text?.Length ?? 0);
        var tools = (harness.ChatClient.FirstOptions?.Tools ?? []).OfType<AIFunction>()
            .Sum(t => t.Name.Length + (t.Description?.Length ?? 0) + JsonSerializer.Serialize(t.JsonSchema).Length);
        var count = (harness.ChatClient.FirstOptions?.Tools ?? []).Count;
        _output?.WriteLine($"design-turn first call: system={system} user={user} tools={tools} ({count} tools) total={system + user + tools}");

        // Measured 2026-11-10 on this fixture: 45654 before the series (26 tools), 28544 after it (12 tools).
        (system + user + tools).Should().BeLessThan(30000, "a first design turn carries only what it can use");
        count.Should().BeLessThanOrEqualTo(12);
    }
}
