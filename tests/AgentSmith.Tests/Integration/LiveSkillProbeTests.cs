using System.Text;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// Live-LLM probe: runs ONE skill end-to-end with real tools against the OpenAI API.
/// Bypasses PipelineContext and PromptComposer assembly — assembles the prompt directly
/// from <see cref="SourceAnchoringPreamble"/> + the inline SKILL.md body so we can A/B
/// the prompt without depending on a published catalog version.
///
/// Skipped unless OPENAI_API_KEY is set. Run manually:
///   OPENAI_API_KEY=sk-... dotnet test --filter "FullyQualifiedName~LiveSkillProbeTests"
/// Cost: ~$0.02 per probe (gpt-4.1, ~1-3 round trips).
/// </summary>
public sealed class LiveSkillProbeTests
{
    private readonly ITestOutputHelper _out;

    public LiveSkillProbeTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Probe_AuthConfigReviewer_OnAuthPortFixture()
    {
        var report = await RunProbe(
            skillName: "auth-config-reviewer",
            phase: SkillExecutionPhase.Review,
            investigatorMode: null,
            skillBody: AuthConfigReviewerSkillBody,
            userPrompt: ReviewPhaseUserPrompt);

        PrintReport(report);
        report.ToolCallCount.Should().BeGreaterThanOrEqualTo(
            2,
            "a review-phase skill with source available must open at least a couple of files; " +
            "if this fires, the production prompt is failing to pull the LLM toward tool use.");
    }

    [Fact]
    public async Task Probe_AuthConfigReviewer_WithProductionStructuredOutputInstruction()
    {
        // Same skill, but appends the actual production structured-output-contributor.md
        // text at the end of the system prompt. Hypothesis: this is what kills tool use
        // in production by reframing the LLM's task as "JSON generator" not "investigator".
        var report = await RunProbe(
            skillName: "auth-config-reviewer",
            phase: SkillExecutionPhase.Review,
            investigatorMode: null,
            skillBody: AuthConfigReviewerSkillBody + "\n\n" + ProductionStructuredOutputContributorInstruction,
            userPrompt: ReviewPhaseUserPrompt);

        PrintReport(report);
        _out.WriteLine($"--- ANALYSIS ---");
        _out.WriteLine($"If ToolCallCount drops from 2 to 0 vs. the plain probe, the production");
        _out.WriteLine($"structured-output instruction is the regression trigger.");
        // Intentionally NOT asserting — we want to observe.
    }

    [Fact]
    public async Task Probe_AuthConfigReviewer_WithProductionSizedUserPrompt()
    {
        // Same skill + same SKILL.md body, but the user prompt is bloated to roughly
        // match production size (~15-20k input tokens) by appending simulated
        // ProjectContext + CodeMap + CompressedScannerFindings + UpstreamObservations.
        // Hypothesis: at this volume the LLM stops using tools and just emits JSON.
        var bigUserPrompt = BuildProductionSizedUserPrompt();
        var report = await RunProbe(
            skillName: "auth-config-reviewer",
            phase: SkillExecutionPhase.Review,
            investigatorMode: null,
            skillBody: AuthConfigReviewerSkillBody,
            userPrompt: bigUserPrompt);

        PrintReport(report);
        _out.WriteLine($"--- ANALYSIS ---");
        _out.WriteLine($"plain probe: 2 tool calls @ ~6k input tokens");
        _out.WriteLine($"this probe:  {report.ToolCallCount} tool calls @ {report.InputTokens} input tokens");
        _out.WriteLine($"If ToolCallCount dropped to 0, prompt SIZE (not content) is the regression trigger.");
    }

    [Fact]
    public async Task Probe_AuthConfigReviewer_WithProductionExactWiring()
    {
        // Mirror SkillCallRuntime + ChatClientFactory EXACTLY:
        //   1. Tools wrapped with TracingAIFunction (SkillCallRuntime.cs:111)
        //   2. Chat client = ChatClientBuilder(bare).UseFunctionInvocation(...).Build() (ChatClientFactory.cs)
        //   3. TracingChatClient wraps everything (SkillCallRuntime.cs:98)
        // If the bug reproduces here, the regression is in one of these three production layers.
        var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
                     ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new SkipProbeException("No LLM credentials.");

        var fixtureRoot = ResolveFixtureRoot();
        var sandbox = new InProcessSandbox(jobId: "probe-prodwiring-" + Guid.NewGuid().ToString("N")[..8],
            workDir: fixtureRoot, logger: NullLogger.Instance);
        var fsHost = new FilesystemToolHost(sandbox, repoPath: fixtureRoot,
            logger: NullLogger<FilesystemToolHost>.Instance);
        var bareTools = fsHost.GetTools(SkillExecutionPhase.Review, null).Cast<AITool>().ToList();

        var trace = new AgentSmith.Application.Services.Loop.LoopTraceCollector();
        var wrappedTools = bareTools
            .Select(t => t is AIFunction f
                ? (AITool)new AgentSmith.Application.Services.Loop.TracingAIFunction(f, trace)
                : t)
            .ToList();

        var baseClient = BuildChatClient();
        var fiClient = new ChatClientBuilder(baseClient)
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = 25)
            .Build();
        var chatClient = new AgentSmith.Application.Services.Loop.TracingChatClient(fiClient, trace);

        var preamble = new SourceAnchoringPreamble().Build();
        var systemPrompt = preamble + "\n\n" + LoadRealSkillBody("api-security", "auth-config-reviewer");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, ReviewPhaseUserPrompt),
        };
        var options = new ChatOptions { Tools = wrappedTools };

        var response = await chatClient.GetResponseAsync(messages, options);

        var toolCallContents = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .ToList();
        var textContents = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<TextContent>()
            .Select(t => t.Text)
            .ToList();
        var traceEntries = trace.Build();

        _out.WriteLine($"=== Probe: auth-config-reviewer (PRODUCTION-EXACT WIRING) ===");
        _out.WriteLine($"Tools offered:          {wrappedTools.Count}");
        _out.WriteLine($"Response FunctionCalls: {toolCallContents.Count}");
        _out.WriteLine($"Trace entries total:    {traceEntries.Count}");
        _out.WriteLine($"Trace tool-call entries:{traceEntries.Count(e => e.Kind == LoopTraceEntryKind.ToolCall)}");
        _out.WriteLine($"Trace LLM entries:      {traceEntries.Count(e => e.Kind == LoopTraceEntryKind.LlmCall)}");
        _out.WriteLine($"ReadSet count:          {trace.ReadSet.Count}");
        _out.WriteLine($"Input tokens:           {response.Usage?.InputTokenCount}");
        _out.WriteLine($"Output tokens:          {response.Usage?.OutputTokenCount}");
        _out.WriteLine("--- Trace entries ---");
        foreach (var e in traceEntries)
            _out.WriteLine($"  [{e.Kind}] {e.ToolName ?? e.ModelName ?? "?"} ({e.DurationMs}ms)" + (e.Success == false ? $" ERROR={e.ErrorMessage}" : ""));
        _out.WriteLine("--- Response FunctionCallContent ---");
        if (toolCallContents.Count == 0)
            _out.WriteLine("  (NONE — bug reproduced!)");
        foreach (var tc in toolCallContents)
            _out.WriteLine($"  {tc.Name}({SerializeArgs(tc.Arguments)})");
        _out.WriteLine("--- Final text content ---");
        var snippet = string.Join("\n", textContents);
        if (snippet.Length > 1500) snippet = snippet[..1500] + "\n…(truncated)";
        _out.WriteLine(snippet);
    }

    [Fact]
    public async Task Probe_AuthConfigReviewer_WithRealCatalogV25SkillBody()
    {
        // Loads the ACTUAL SKILL.md from the cached v2.5.0 catalog the user's
        // CLI downloaded — strips YAML frontmatter, uses just the prompt body.
        // Hypothesis: the inline shorthand I had in this test file doesn't
        // exactly match what production sees; the real body has the "validator
        // will downgrade ... finding survives either way" wording that might
        // be the regression trigger.
        var realBody = LoadRealSkillBody("api-security", "auth-config-reviewer");
        var report = await RunProbe(
            skillName: "auth-config-reviewer",
            phase: SkillExecutionPhase.Review,
            investigatorMode: null,
            skillBody: realBody,
            userPrompt: ReviewPhaseUserPrompt);

        PrintReport(report);
        _out.WriteLine($"--- ANALYSIS ---");
        _out.WriteLine($"This uses the EXACT SKILL.md body from cached catalog v2.5.0.");
        _out.WriteLine($"If tool-call count drops to 0 here, the published SKILL.md is the regression trigger.");
    }

    private static string LoadRealSkillBody(string catalog, string skill)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "agentsmith", "skills", "skills", catalog, skill, "SKILL.md");
        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"Real SKILL.md not found at {path}. Run a pipeline once to populate the cache, " +
                "or set AGENTSMITH_SKILL_CACHE to point to a populated cache directory.");
        var raw = File.ReadAllText(path);
        // strip YAML frontmatter (between --- markers)
        if (raw.StartsWith("---"))
        {
            var endOfFrontmatter = raw.IndexOf("---", 3, StringComparison.Ordinal);
            if (endOfFrontmatter > 0)
                raw = raw[(endOfFrontmatter + 3)..].TrimStart('\n', '\r');
        }
        return raw;
    }

    [Fact]
    public async Task Probe_ApiVulnAnalystPlanner_OnAuthPortFixture()
    {
        var report = await RunProbe(
            skillName: "api-vuln-analyst-planner",
            phase: SkillExecutionPhase.Plan,
            investigatorMode: null,
            skillBody: ApiVulnAnalystPlannerSkillBody,
            userPrompt: PlanPhaseUserPrompt);

        PrintReport(report);
        report.ToolCallCount.Should().BeGreaterThanOrEqualTo(
            1,
            "a plan-phase recon skill should make at least one tool call for repo-layout discovery.");
    }

    private async Task<ProbeReport> RunProbe(
        string skillName,
        SkillExecutionPhase phase,
        string? investigatorMode,
        string skillBody,
        string userPrompt)
    {
        var fixtureRoot = ResolveFixtureRoot();
        var sandbox = new InProcessSandbox(jobId: "probe-" + Guid.NewGuid().ToString("N")[..8], workDir: fixtureRoot, logger: NullLogger.Instance);
        var fsHost = new FilesystemToolHost(sandbox, repoPath: fixtureRoot, logger: NullLogger<FilesystemToolHost>.Instance);
        var tools = fsHost.GetTools(phase, investigatorMode).Cast<AITool>().ToList();

        var preamble = new SourceAnchoringPreamble().Build();
        var systemPrompt = preamble + "\n\n" + skillBody;

        // Wrap with FunctionInvokingChatClient so tool calls auto-loop (same pattern as production)
        var baseClient = BuildChatClient();
        var chatClient = new FunctionInvokingChatClient(baseClient)
        {
            MaximumIterationsPerRequest = 8,
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };
        var chatOptions = new ChatOptions { Tools = tools };

        var response = await chatClient.GetResponseAsync(messages, chatOptions);

        var toolCallContents = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .ToList();
        var textContents = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<TextContent>()
            .Select(t => t.Text)
            .ToList();

        return new ProbeReport(
            SkillName: skillName,
            Phase: phase,
            ToolsOffered: tools.Count,
            ToolCallCount: toolCallContents.Count,
            ToolCalls: toolCallContents.Select(c => $"{c.Name}({SerializeArgs(c.Arguments)})").ToList(),
            FinalTextSnippet: string.Join("\n", textContents),
            InputTokens: response.Usage?.InputTokenCount ?? -1,
            OutputTokens: response.Usage?.OutputTokenCount ?? -1,
            SystemPromptLength: systemPrompt.Length,
            UserPromptLength: userPrompt.Length);
    }

    /// <summary>
    /// Build an IChatClient against whatever provider is configured in env. Tries
    /// Azure OpenAI first (the user's primary), falls back to public OpenAI.
    /// </summary>
    private static IChatClient BuildChatClient()
    {
        var azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

        if (!string.IsNullOrWhiteSpace(azureKey) &&
            !string.IsNullOrWhiteSpace(azureEndpoint) &&
            !string.IsNullOrWhiteSpace(azureDeployment))
        {
            var agent = new AgentConfig
            {
                Type = "azure_openai",
                Endpoint = azureEndpoint,
                Deployment = azureDeployment
            };
            var assignment = new ModelAssignment
            {
                Model = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL") ?? "gpt-4.1",
                Deployment = azureDeployment
            };
            return new OpenAiChatClientBuilder().Build(agent, assignment);
        }

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            var agent = new AgentConfig { Type = "openai" };
            var assignment = new ModelAssignment { Model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4.1" };
            return new OpenAiChatClientBuilder().Build(agent, assignment);
        }

        throw new SkipProbeException(
            "No LLM credentials in env. Set either:\n" +
            "  AZURE_OPENAI_API_KEY + AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_DEPLOYMENT (+ optional AZURE_OPENAI_MODEL)\n" +
            "  or OPENAI_API_KEY (+ optional OPENAI_MODEL, default gpt-4.1)\n" +
            "Then re-run.");
    }

    private static string SerializeArgs(IDictionary<string, object?>? args)
    {
        if (args is null) return "";
        try { return JsonSerializer.Serialize(args); }
        catch { return "<unserializable>"; }
    }

    private void PrintReport(ProbeReport r)
    {
        _out.WriteLine($"=== Probe: {r.SkillName} (phase={r.Phase}) ===");
        _out.WriteLine($"Tools offered:      {r.ToolsOffered}");
        _out.WriteLine($"Tool-call requests: {r.ToolCallCount}");
        _out.WriteLine($"Input tokens:       {r.InputTokens}");
        _out.WriteLine($"Output tokens:      {r.OutputTokens}");
        _out.WriteLine($"System prompt len:  {r.SystemPromptLength} chars");
        _out.WriteLine($"User prompt len:    {r.UserPromptLength} chars");
        _out.WriteLine("--- Tool calls (in first response turn) ---");
        if (r.ToolCalls.Count == 0)
            _out.WriteLine("  (NONE — LLM emitted observations directly, no tools used)");
        foreach (var tc in r.ToolCalls)
            _out.WriteLine($"  {tc}");
        _out.WriteLine("--- Final text content ---");
        var snippet = r.FinalTextSnippet.Length > 1500
            ? r.FinalTextSnippet[..1500] + "\n…(truncated)"
            : r.FinalTextSnippet;
        _out.WriteLine(snippet);
    }

    private static string ResolveFixtureRoot()
    {
        // Walk up from test bin/ to repo root; fixtures live under tests/.../Integration/Fixtures/AuthPortLike
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tests")))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("Could not locate repo root from test bin path.");
        return Path.Combine(dir.FullName, "tests", "AgentSmith.Tests", "Integration", "Fixtures", "AuthPortLike");
    }

    private sealed record ProbeReport(
        string SkillName,
        SkillExecutionPhase Phase,
        int ToolsOffered,
        int ToolCallCount,
        IReadOnlyList<string> ToolCalls,
        string FinalTextSnippet,
        long InputTokens,
        long OutputTokens,
        int SystemPromptLength,
        int UserPromptLength);

    private sealed class SkipProbeException(string message) : Exception(message);

    // ─────────────────────────────────────────────────────────────────────────
    //  Inline SKILL.md bodies (frontmatter stripped — only the prompt content)
    //  Edit these directly in the test file to A/B prompt variants without
    //  re-publishing the catalog.
    // ─────────────────────────────────────────────────────────────────────────

    private const string AuthConfigReviewerSkillBody = """
        You review the authentication and authorization configuration directly in source.
        You only run when source is available (api_source_available: true).

        ## What to flag

        ### Disabled JWT validation
        - `ValidateLifetime = false` — accepts expired tokens forever
        - `ValidateIssuerSigningKey = false` — accepts unsigned / foreign-signed tokens
        - `ValidateIssuer = false` — accepts tokens from any issuer
        - `ValidateAudience = false` — token replay across applications

        ### Dead or missing middleware
        - `services.AddAuthentication(...)` configured but `app.UseAuthentication()` missing → no auth runs
        - `app.UseAuthorization()` placed after endpoint mapping → enforcement never reached

        ### Unsafe CORS
        - `AllowAnyOrigin().AllowCredentials()` together — flag even though browsers block, the intent is wrong
        - `Access-Control-Allow-Origin: *` set explicitly with cookies in scope

        ### Missing security headers
        - No middleware adding `Strict-Transport-Security`, `X-Content-Type-Options`, `Content-Security-Policy`

        ## Output

        Per the framework observation schema. concern: "security", set file + start_line to the source location, evidence_mode: "analyzed_from_source" when you opened the file via read_file. For absence findings (no UseHsts anywhere) use evidence_mode: "potential" with file: null.

        JSON only, no preamble. Output: { "observations": [ { "concern": "security", "description": "...", "severity": "high", "confidence": 90, "file": "src/Program.cs", "start_line": 12, "evidence_mode": "analyzed_from_source" } ] }
        """;

    private const string ApiVulnAnalystPlannerSkillBody = """
        You lead OWASP API Security Top 10 (2023) triage of the Nuclei scan output. Your
        observations set the OWASP-categorisation baseline that other analysts and the
        final-phase filter compare against.

        ## Phase 1 — API Context (do this first)

        Before analysing findings, explore the target API to understand:
        - Which authentication scheme is in use (OAuth2, API keys, JWT, session cookies)
        - Existing authorization patterns (middleware, decorators, policy-based)
        - Input validation approach (model binding, schema validation, manual checks)
        - API versioning and deprecation patterns

        Spend your first 3-5 tool calls on this.

        ## Phase 2 — Finding Analysis

        Map each valid finding to OWASP API Security Top 10 (2023). Cite endpoint, HTTP method, and impact.

        ## Output

        JSON only, single line. Output: { "observations": [ { "concern": "security", "category": "API2:2023", "api_path": "POST /api/x", "description": "...", "severity": "high", "confidence": 90, "evidence_mode": "analyzed_from_source", "file": "src/Program.cs", "start_line": 12 } ] }
        """;

    /// <summary>Builds a user prompt that simulates the size/shape Production assembles
    /// (CompressApiScanFindings 1357 entries + ProjectContext + CodeMap + UpstreamObservations).</summary>
    private static string BuildProductionSizedUserPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Project context");
        sb.AppendLine();
        sb.AppendLine("Small .NET 8 Web API similar to an identity-token gateway. ASP.NET Core minimal hosting.");
        sb.AppendLine("Single solution, source available, language=C#. Domain: identity & permission management.");
        sb.AppendLine("Architecture: layered (API / Application / Domain / Infrastructure.Persistence). MediatR-based CQRS.");
        sb.AppendLine();
        sb.AppendLine("### Code map (top modules)");
        sb.AppendLine();
        for (int i = 0; i < 60; i++)
            sb.AppendLine($"- Module{i}: Controller / Application / Infrastructure paths summarized (200-1500 LoC).");
        sb.AppendLine();
        sb.AppendLine("### Compressed scanner findings (1357 entries → 4 category slices + 38 top anchors)");
        sb.AppendLine();
        sb.AppendLine("**Spectral (schema-level, 768 errors / 390 warnings)**");
        for (int i = 0; i < 200; i++)
            sb.AppendLine($"- spectral-{i:D3}: GET /api/resource/{i} — missing maxLength on field 'name' (RFC 7807)");
        sb.AppendLine();
        sb.AppendLine("**Nuclei (header-level, 195 informational)**");
        for (int i = 0; i < 80; i++)
            sb.AppendLine($"- nuclei-{i:D3}: header check — Strict-Transport-Security missing on GET /api/x{i}");
        sb.AppendLine();
        sb.AppendLine("**ZAP (4 findings, 1 low)**");
        sb.AppendLine("- zap-001: Cookie attributes (low) — Set-Cookie without SameSite on /api/session");
        sb.AppendLine("- zap-002 / 003 / 004: informational headers");
        sb.AppendLine();
        sb.AppendLine("### Prior-round observations on the bus");
        sb.AppendLine();
        sb.AppendLine("- inventory-auth-stack (potential): cited Program.cs as the file configuring JWT bearer");
        sb.AppendLine("- api-design-auditor (potential): schema-level findings around missing constraints (50+ items)");
        sb.AppendLine("- auth-tester (potential): schema-level JWT analysis — issuer/audience claims not documented");
        sb.AppendLine("- api-vuln-analyst-investigator (potential): OWASP API4:2023 risk from missing rate-limit docs");
        sb.AppendLine();
        sb.AppendLine("### Your task");
        sb.AppendLine();
        sb.AppendLine("Review the authentication and authorization configuration. Emit your observations now.");
        return sb.ToString();
    }

    // Verbatim copy of src/AgentSmith.Application/Prompts/Resources/structured-output-contributor.md
    // — the actual instruction Production appends. Hypothesis: this is the regression trigger.
    private const string ProductionStructuredOutputContributorInstruction = """
        Respond with a JSON array of findings. Each finding: { "file": "", "line": 0, "title": "", "severity": "", "details": "", "apiPath": "METHOD /path", "schemaName": "SchemaName" }. Use apiPath for endpoint-level findings and schemaName for schema-level findings. Omit both for file-based findings. Max 50 items. Output minified JSON on a single line — no whitespace between tokens, no indentation, no newlines.
        """;

    // ─────────────────────────────────────────────────────────────────────────
    //  Mock user prompts — what the PromptComposer would build from PipelineContext
    // ─────────────────────────────────────────────────────────────────────────

    private const string ReviewPhaseUserPrompt = """
        ### Project context

        Small .NET 8 Web API similar to an identity-token gateway. Single solution,
        ASP.NET Core minimal hosting model. Source available, language=C#.

        ### Scan summary

        Nuclei: 0 critical / 0 high / 0 medium findings (mostly info-level header checks).
        Spectral: 768 errors, 390 warnings on the swagger spec (missing rate-limit
        documentation, schema constraints, error response definitions).
        ZAP api-scan: 1 low (cookie attribute), 3 informational.

        ### Prior-round observations on the bus

        - inventory-auth-stack: cited Program.cs as the file configuring JWT bearer
        - api-design-auditor: schema-only findings around missing constraints (potential)
        - auth-tester: schema-only JWT analysis (potential)

        Review the authentication configuration. Emit your observations now.
        """;

    private const string PlanPhaseUserPrompt = """
        ### Project context

        Small .NET 8 Web API similar to an identity-token gateway. Single solution,
        source available at the sandbox repo root.

        ### Scan summary

        Nuclei: 195 informational hits on header checks, no critical/high/medium.
        Spectral: 768 schema errors, 390 warnings.
        ZAP: 4 findings (1 low).

        ### Your task

        Set the OWASP API Security Top 10 (2023) baseline. Read the layout, then categorize.
        Emit your observations now.
        """;
}
