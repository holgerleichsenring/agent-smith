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

        // Measured 2026-11-10 on this fixture: 45654 before the series (26 tools), 31857 after it (16 tools — the mock,
        // render and compare tools stay on every design turn, since a mock or a public URL needs no upload).
        // 2026-11-10-d854a: 33745 with state_open_questions (17 tools) — the questions' card needs the tool on every
        // design turn, since a draft can come on the first.
        // 2026-11-12-cd4ca: 35466 with the proposal write tools (22 tools) — the draft is written, not replied.
        // 2026-11-12-cd4cg: 36329 with outcome_format gone (21 tools) and the schemas in the write tools' descriptions,
        // which no turn fetches any more (an epic format alone was 12000 characters per fetch).
        // 2026-11-12-cd4cj/cd4ck: review_proposal, and read_notes/write_notes in place of note_reference (23 tools).
        // 2026-11-17-5701d: 39053 with a ticket's specs under draft:tickets/<key>/ in the write_file format (5701a) and
        // design-partner-master 3.4.0 — the text that tells the master where a ticket's specs go and that filing puts
        // them on the ticket's branch.
        // 2026-11-18-550ac: 38588 with design-partner-master 3.5.0 (system 17083, tools 20583, 22 tools) — the cut by
        // theme replaced the merge and branch-state tests.
        // 2026-11-20-4cfa: 40828 with design-partner-master 3.8.2 — the replied draft is reviewed as it stands, a recut
        // deletes what it replaces and checks every ticket has a spec, delete_file discards the draft.
        (system + user + tools).Should().BeLessThan(42000, "a first design turn carries only what it can use");
        // 2026-11-15-bf77: offer_filing (24 tools, 37268) — File appears because the master offered the draft.
        // 2026-11-18-9c1ca: review_proposal gone (23 at most).
        count.Should().BeLessThanOrEqualTo(23);
    }
}
