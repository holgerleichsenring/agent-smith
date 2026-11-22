using AgentSmith.Application.Services.Specs.Revision;
using AgentSmith.PipelineHarness.Composition;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-19-6b0ba: the coding master on a stand-in, scripted — it re-emits the spec its prompt carries with a goal of
/// its own and records it with revise_spec, then ends on its report; or (2026-11-22-96c8a) it asks with ask_human first.
/// </summary>
internal static class SpecWritingScript
{
    public const string WrittenGoal = "Answer an empty request body with 400 at the API edge";

    public static void WritesTheSpec(RealCompositionHarness harness) =>
        harness.ChatClient
            .EnqueueDeferred(() => Revise(harness))
            .EnqueueText("## Written\nThe spec now says what the work is.");

    public static void AsksFirst(RealCompositionHarness harness, string question) =>
        harness.ChatClient
            .EnqueueToolCall("ask_human", System.Text.Json.JsonSerializer.Serialize(new { question }))
            .EnqueueText(string.Empty);

    // The stub counts every written file as staged; the published stand-in is a commit in a real checkout, so the
    // index is answered as git would and the gate that keeps staged work out of the spec commit admits the revision.
    private static ChatResponse Revise(RealCompositionHarness harness)
    {
        harness.StubSandboxFactory!.GitAnswers["diff --cached --name-only"] = (0, string.Empty);
        var prompt = harness.ChatClient.CallMessages[^1].Select(m => m.Text ?? string.Empty)
            .First(t => t.Contains("```yaml", StringComparison.Ordinal));
        var yaml = System.Text.RegularExpressions.Regex.Match(
            prompt, "```yaml\\n(.*?)\\n```", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        var goal = yaml.Split('\n').First(l => l.StartsWith("goal:", StringComparison.Ordinal));
        var args = new Dictionary<string, object?>
        {
            ["spec_yaml"] = yaml.Replace(goal, $"goal: \"{WrittenGoal}\"", StringComparison.Ordinal),
            ["reason"] = "the ticket stood in for its spec",
        };
        return new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call_revise", ReviseSpecToolHost.Name, args)]));
    }
}
