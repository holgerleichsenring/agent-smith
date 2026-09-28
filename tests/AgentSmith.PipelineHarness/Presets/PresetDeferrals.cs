using AgentSmith.PipelineHarness.Llm;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 console-runner support. Mirrors the xUnit suite's per-preset
/// shape so <c>dotnet run -- --preset &lt;name&gt;</c> exercises the same
/// coverage as the test runner. p0199f moved scanner stubs into
/// RealCompositionHarness defaults so the only override left here is the
/// init-project analyzer stub.
/// </summary>
internal static class PresetDeferrals
{
    // init-project needs the LLM-driven analyzer swapped for the stub so the
    // ScriptedChatClient queue isn't drained by ProjectAnalyzer before
    // BootstrapRound runs.
    public static Action<IServiceCollection>? ComposeOverrides(string preset) =>
        NeedsStubAnalyzer(preset) ? HarnessProjectAnalyzerStub.Register : null;

    private static bool NeedsStubAnalyzer(string preset) =>
        string.Equals(preset, "init-project", StringComparison.OrdinalIgnoreCase);

    public static void SeedDefaultScript(string preset, ScriptedChatClient client)
    {
        switch (preset.ToLowerInvariant())
        {
            // 2026-09-25-a7e8: the four coding names were four cases with one body long
            // before p0393 collapsed them into `code` — nothing scenario-specific was
            // ever scripted here, so the collapse costs no coverage. The docker tier is
            // where the three coding scenarios differ, and DockerPresetScripts keeps them.
            case "code":
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
                    """{"path":"primary/.agentsmith/contexts/default/principles.md","content":"# Harness fixture coding principles"}""");
                client.EnqueueText("Bootstrap files written.");
                break;
            default:
                client.EnqueueText("{}");
                break;
        }
    }
}
