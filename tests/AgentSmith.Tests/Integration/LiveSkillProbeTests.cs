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
