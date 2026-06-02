using AgentSmith.PipelineHarness.Llm;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c: single source of truth for the per-preset ScriptedChatClient
/// FIFO seed that drives the docker-tier happy path. Each preset's master
/// receives the minimal tool-call sequence that lets the production handler
/// chain (CommitAndPR / DeliverFindings / DeliverOutput / etc.) produce
/// observable artefacts inside the per-test bare git remote and the
/// container working tree. Used by both the xUnit tests under Presets/
/// and the standalone DockerPresetRunner so the two entry points cannot
/// drift.
///
/// Conventions:
///   - All file paths land under <c>primary/</c> — the docker harness's
///     repo name (see <see cref="DockerHarnessRepo"/>). Without that prefix
///     FilesystemToolHost routes the write to the wrong sandbox.
///   - The trailing EnqueueText closes the agentic loop; the empty queue
///     would otherwise loop until the iteration cap.
/// </summary>
internal static class DockerPresetScripts
{
    public static void Seed(string preset, ScriptedChatClient client)
    {
        switch (preset.ToLowerInvariant())
        {
            case "fix-bug":
                SeedFixBug(client);
                break;
            case "fix-no-test":
                SeedFixNoTest(client);
                break;
            case "add-feature":
                SeedAddFeature(client);
                break;
            case "security-scan":
                SeedSecurityScan(client);
                break;
            case "api-security-scan":
                SeedApiSecurityScan(client);
                break;
            case "mad-discussion":
                SeedMadDiscussion(client);
                break;
            case "legal-analysis":
                SeedLegalAnalysis(client);
                break;
            default:
                client.EnqueueText("{}");
                break;
        }
    }

    private static void SeedFixBug(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/NOTE.md","content":"docker-harness-fix-bug"}""")
        .EnqueueText("Edit applied.");

    private static void SeedFixNoTest(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/QUICK.md","content":"docker-harness-fix-no-test"}""")
        .EnqueueText("Quick fix applied.");

    private static void SeedAddFeature(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/FEATURE.md","content":"docker-harness-add-feature"}""")
        .EnqueueText("Feature added.");

    private static void SeedSecurityScan(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/.agentsmith/security/scan.md","content":"# Findings\nharness-scan"}""")
        .EnqueueText("Scan synthesised.");

    private static void SeedApiSecurityScan(ScriptedChatClient client) =>
        client.EnqueueText("No findings.");

    private static void SeedMadDiscussion(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/discussions/mad-discussion.md","content":"# Synthesis\nharness"}""")
        .EnqueueText("Discussion synthesised.");

    private static void SeedLegalAnalysis(ScriptedChatClient client) => client
        .EnqueueText("nda")
        .EnqueueToolCall("write_file",
            """{"path":"primary/output/legal-findings.md","content":"# Findings\nharness"}""")
        .EnqueueText("Analysis complete.");
}
