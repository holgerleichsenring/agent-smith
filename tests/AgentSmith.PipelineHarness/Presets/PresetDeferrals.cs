using AgentSmith.PipelineHarness.Llm;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 console-runner support. Mirrors the xUnit suite's per-preset
/// shape so <c>dotnet run -- --preset &lt;name&gt;</c> exercises the same
/// coverage as the test runner. p0199f moved scanner stubs into
/// RealCompositionHarness defaults so the only override left here is the
/// init-project / autonomous analyzer stub.
/// </summary>
internal static class PresetDeferrals
{
    private static readonly Dictionary<string, string> Deferred =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["skill-manager"] =
                "Preset has deeper shape issues than p0204 fixed — LoadContext removed but CompileDiscussion (and likely others) still require Repository the preset never provides. Full rework p0204a.",
        };

    public static bool IsDeferred(string preset, out string reason) =>
        Deferred.TryGetValue(preset, out reason!);

    // p0199d: init-project + autonomous need the LLM-driven analyzer
    // swapped for the stub so the ScriptedChatClient queue isn't drained
    // by ProjectAnalyzer before BootstrapRound / SkillRound run.
    public static Action<IServiceCollection>? ComposeOverrides(string preset) =>
        NeedsStubAnalyzer(preset) ? HarnessProjectAnalyzerStub.Register : null;

    private static bool NeedsStubAnalyzer(string preset) =>
        string.Equals(preset, "init-project", StringComparison.OrdinalIgnoreCase)
        || string.Equals(preset, "autonomous", StringComparison.OrdinalIgnoreCase);

    public static void SeedDefaultScript(string preset, ScriptedChatClient client)
    {
        switch (preset.ToLowerInvariant())
        {
            case "fix-bug":
            case "fix-no-test":
            case "add-feature":
            case "mad-discussion":
                client.EnqueueText("No changes needed.");
                break;
            case "security-scan":
            case "api-security-scan":
                client.EnqueueText("No findings.");
                break;
            case "legal-analysis":
                // BootstrapDocument's contract-classifier consumes one
                // response before the master takes over. Mirror the
                // LegalAnalysisTests script so the standalone runner
                // matches the xUnit shape.
                client.EnqueueText("nda");
                client.EnqueueToolCall("write_file",
                    """{"path":"primary/output/legal-findings.md","content":"# Findings"}""");
                client.EnqueueText("Analysis complete.");
                break;
            case "init-project":
                client.EnqueueToolCall("write_file",
                    """{"path":"primary/.agentsmith/contexts/default/coding-principles.md","content":"# Harness fixture coding principles"}""");
                client.EnqueueText("Bootstrap files written.");
                break;
            case "autonomous":
                client.EnqueueText("{}");
                client.EnqueueText("{}");
                break;
            default:
                client.EnqueueText("{}");
                break;
        }
    }
}
