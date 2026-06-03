using AgentSmith.PipelineHarness.Llm;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 console-runner support. Mirrors the xUnit suite's per-preset
/// shape so <c>dotnet run -- --preset &lt;name&gt;</c> exercises the same
/// coverage as the test runner. Per-preset script scaffolding lives in
/// the xUnit test files; this table keeps the runner thin (preset →
/// scripted-LLM seeder, preset → boundary-swap callback).
/// </summary>
internal static class PresetDeferrals
{
    private static readonly Dictionary<string, string> Deferred =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["init-project"] =
                "BootstrapDiscover needs AvailableRoles + a real RemoteContextDiscovery (p0199d).",
            ["autonomous"] =
                "Triage demands non-empty AvailableRoles loaded from a real skill catalog (p0199d).",
            ["skill-manager"] =
                "Preset has deeper shape issues than p0204 fixed — LoadContext removed but CompileDiscussion (and likely others) still require Repository the preset never provides. Full rework p0204a.",
        };

    public static bool IsDeferred(string preset, out string reason) =>
        Deferred.TryGetValue(preset, out reason!);

    public static Action<IServiceCollection>? RegisterScannerStubsIfNeeded(string preset) =>
        string.Equals(preset, "api-security-scan", StringComparison.OrdinalIgnoreCase)
            ? ApiScannerStubs.Register
            : null;

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
            default:
                client.EnqueueText("{}");
                break;
        }
    }
}
