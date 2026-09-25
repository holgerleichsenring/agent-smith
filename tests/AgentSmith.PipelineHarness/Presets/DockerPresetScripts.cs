using AgentSmith.PipelineHarness.Llm;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c: single source of truth for the ScriptedChatClient FIFO seeds that drive the
/// docker-tier happy path. Each script gives a master the minimal tool-call sequence that
/// lets the production handler chain (CommitAndPR / DeliverFindings / DeliverOutput / …)
/// produce observable artefacts inside the per-test bare git remote and the container
/// working tree. Both the xUnit suites under Presets/ and the standalone
/// DockerPresetRunner seed from here, so the two entry points cannot drift.
///
/// 2026-09-25-a7e8: a script is chosen by SCENARIO, not by preset. The two were one string
/// while fixing a bug, fixing without tests and adding a feature were presets of their own;
/// p0393 collapsed all three onto <c>code</c>, so keying the scripts on the preset name
/// would leave one script where there are three scenarios and drop two of them without a
/// single test turning red. A suite therefore NAMES its script and passes it in;
/// <see cref="DefaultFor"/> serves the one caller that has only a preset to go on — the
/// console runner, which is handed a name on the command line.
///
/// Conventions:
///   - All file paths land under <c>primary/</c> — the docker harness's repo name (see
///     <see cref="DockerHarnessRepo"/>). Without that prefix FilesystemToolHost routes
///     the write to the wrong sandbox.
///   - The trailing EnqueueText closes the agentic loop; the empty queue would otherwise
///     loop until the iteration cap.
/// </summary>
internal static class DockerPresetScripts
{
    /// <summary>The script a preset runs when the caller named no scenario. <c>code</c>
    /// answers with the bug-fix one, the scenario the docker tier was built around.</summary>
    public static Action<ScriptedChatClient> DefaultFor(string preset) =>
        preset.ToLowerInvariant() switch
        {
            "code" => BugFix,
            "security-scan" => SecurityScan,
            "api-security-scan" => ApiSecurityScan,
            "mad-discussion" => MadDiscussion,
            "legal-analysis" => LegalAnalysis,
            "init-project" => InitProject,
            "autonomous" => Autonomous,
            _ => client => client.EnqueueText("{}"),
        };

    /// <summary>
    /// Fixing a bug: one edit, then the loop closes. p0394a: GeneratePlan left the
    /// phase path — the spec is the plan, so the master's write_file tool-call is the
    /// first coding-side FIFO consumer and no plan slot is scripted anymore.
    /// </summary>
    public static void BugFix(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/NOTE.md","content":"docker-harness-bug-fix"}""")
        .EnqueueText("Edit applied.");

    /// <summary>A fix whose scenario writes no test of its own.</summary>
    public static void FixWithoutTests(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/QUICK.md","content":"docker-harness-fix-without-tests"}""")
        .EnqueueText("Quick fix applied.");

    /// <summary>Adding a feature: the post-master GenerateTests / Test / GenerateDocs chain.</summary>
    public static void NewFeature(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/FEATURE.md","content":"docker-harness-new-feature"}""")
        .EnqueueText("Feature added.");

    private static void SecurityScan(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/.agentsmith/security/scan.md","content":"# Findings\nharness-scan"}""")
        .EnqueueText("Scan synthesised.");

    // p0199f: api-security-master writes the consolidated findings into
    // primary/.agentsmith/api-security/scan.md (same shape as security-scan) then closes
    // the loop. DeliverFindings ships the file from the sandbox working tree; the test
    // asserts on chain shape (write_file path), not master prose. Single write keeps the
    // docker-tier passive run fast — multi-step master investigations are skill quality,
    // not pipeline wiring, and belong on the fast tier.
    private static void ApiSecurityScan(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/.agentsmith/api-security/scan.md","content":"# Findings\nharness-api-scan"}""")
        .EnqueueText("Scan synthesised.");

    private static void MadDiscussion(ScriptedChatClient client) => client
        .EnqueueToolCall("write_file",
            """{"path":"primary/discussions/mad-discussion.md","content":"# Synthesis\nharness"}""")
        .EnqueueText("Discussion synthesised.");

    private static void LegalAnalysis(ScriptedChatClient client) => client
        .EnqueueText("nda")
        .EnqueueToolCall("write_file",
            """{"path":"primary/output/legal-findings.md","content":"# Findings\nharness"}""")
        .EnqueueText("Analysis complete.");

    // p0199d: BootstrapRound for csharp-bootstrap fires one WriteFile to principles.md
    // (the bootstrap surface forbids context.yaml via write_file and the fixture skill is
    // intentionally minimal) so the "0 changes" guard stays green.
    // 2026-09-23-9bb2: a re-init runs the discovery round like any other init, so its
    // answer is the first thing the FIFO serves — before the bootstrap round's writes.
    private static void InitProject(ScriptedChatClient client) => client
        .EnqueueText(
            """
            {
              "status": "complete",
              "components": [
                { "name": "default", "workdir": ".", "language": "csharp", "evidence": "Fixture.csproj" }
              ]
            }
            """)
        .EnqueueToolCall("write_file",
            """{"path":"primary/.agentsmith/contexts/default/principles.md","content":"# Harness fixture coding principles"}""")
        .EnqueueText("Bootstrap files written.");

    // p0199d: Triage routes to autonomous-planner + autonomous-investigator; each round
    // closes on its first text response so the queue keeps two entries. Skill output is
    // not asserted — the docker-tier test pins handler-chain shape, not LLM quality.
    private static void Autonomous(ScriptedChatClient client) => client
        .EnqueueText("{}")
        .EnqueueText("{}");
}
