using System.Net;
using System.Text.RegularExpressions;
using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0397: the LiveLLM plan-call golden — answers "does the multi-repo plan
/// call fit, parse, and cover every scoped repo?" without a live pipeline run.
/// Composes the REAL production prompts (AgentPromptBuilder + the embedded
/// agent-plan-system template, two repo code maps, a migration-manual-sized
/// ticket) and calls each available model at each output cap in
/// <see cref="OutputCaps"/> — the live failure mode this pins: a large 2-repo
/// migration ticket fills MaxOutputTokens=8192 EXACTLY and the truncated JSON
/// parses to 0 steps. Same gating as the expectation golden: Category=LiveLLM
/// plus a loud runtime skip per missing credential. Ticket text comes from
/// AGENTSMITH_EVAL_TICKET_FILE (local file, HTML or plain text — NEVER
/// committed) or falls back to the committed synthetic fixture. The report
/// lands in Reports/plan-goldens/ — commit only reports produced from the
/// synthetic fixture; a real-ticket report may echo customer identifiers.
/// </summary>
[Trait("Category", "LiveLLM")]
public sealed class PlanCallLiveEvalTests(ITestOutputHelper output)
{
    private const string ServerRepo = "SampleServer";
    private const string WorkerRepo = "SampleWorker";
    private static readonly int[] OutputCaps = [8192, 16384];

    private const string CodingPrinciples = """
        - SOLID, DRY, YAGNI; model responsibilities, don't move code.
        - Constructor injection, transient lifetime by default; no service locator.
        - Every new public method gets at least one test.
        - No wrapper/adapter shims: migrate callers directly to the new API.
        - English-only identifiers, comments, and log messages.
        """;

    [Fact]
    public async Task PlanCall_ModelByOutputCapMatrix_FitsParsesAndCoversBothRepos()
    {
        var clients = BuildClients();
        if (clients.Count == 0)
        {
            output.WriteLine("SKIP: no AZURE_OPENAI_API_KEY / OPENAI_API_KEY / ANTHROPIC_API_KEY "
                + "in env — the eval tier is paid-API and opt-in.");
            return;
        }

        var (source, ticket) = LoadTicket();
        var builder = new AgentPromptBuilder(EmbeddedCatalog());
        var system = builder.BuildPlanSystemPrompt(CodingPrinciples, CodeMaps());
        var user = builder.BuildPlanUserPrompt(ticket, ProjectMaps());
        output.WriteLine($"Ticket source: {source}; system {system.Length} chars, "
            + $"user {user.Length} chars.");

        var rows = new List<PlanCallEvalReport.Row>();
        foreach (var (model, client) in clients)
            foreach (var cap in OutputCaps)
                rows.Add(await EvaluateAsync(client, model, cap, system, user));

        var report = new PlanCallEvalReport(
            source, system.Length, user.Length, DateTimeOffset.UtcNow, rows);
        var mdPath = PlanCallEvalReportWriter.Write(report, ReportsDirectory());
        output.WriteLine($"Report: {mdPath}");

        // Mechanics always assert: every configured combo produced a row and
        // both report artifacts exist.
        rows.Should().HaveCount(clients.Count * OutputCaps.Length);
        File.Exists(mdPath).Should().BeTrue();
        File.Exists(Path.ChangeExtension(mdPath, ".json")).Should().BeTrue();

        // The quality gate — deliberately the weakest useful claim so the eval
        // stays a regression net, not a flake: SOME model × cap combo must
        // yield a PARSED plan with steps covering both scoped repos.
        rows.Should().Contain(
            r => r.ParsedSteps > 0 && r.BothReposCovered,
            "at least one model x cap combo must produce a parsed multi-repo plan; report:\n{0}",
            File.ReadAllText(mdPath));
    }

    private async Task<PlanCallEvalReport.Row> EvaluateAsync(
        IChatClient client, string model, int cap, string system, string user)
    {
        output.WriteLine($"Calling {model} @ MaxOutputTokens={cap} ...");
        try
        {
            // Mirrors GeneratePlanHandler.CallPlannerAsync: same message shape,
            // same ResponseFormat=Json enforcement (p0340), cap swapped per run.
            var response = await client.GetResponseAsync(
                [new(ChatRole.System, system), new(ChatRole.User, user)],
                new ChatOptions { MaxOutputTokens = cap, ResponseFormat = ChatResponseFormat.Json });
            var text = response.Text ?? string.Empty;
            var parsed = TryParse(model, text);
            var salvaged = parsed is null ? Parser().SalvageProse(text) : null;
            var plan = parsed ?? salvaged!;
            var row = new PlanCallEvalReport.Row(
                model, cap,
                response.FinishReason?.ToString() ?? "unknown",
                response.Usage?.OutputTokenCount,
                text.Length,
                parsed?.Steps.Count,
                salvaged?.Steps.Count,
                CoversBothRepos(plan),
                plan.Steps.Select(s => s.TargetFile?.Value ?? s.Description).Take(8).ToList(),
                Error: null);
            output.WriteLine($"  -> finish={row.FinishReason} outTokens={row.OutputTokens} "
                + $"parsed={row.ParsedSteps?.ToString() ?? "FAILED"} "
                + $"salvaged={row.SalvagedSteps?.ToString() ?? "-"} bothRepos={row.BothReposCovered}");
            return row;
        }
        catch (Exception ex)
        {
            output.WriteLine($"  -> ERROR: {ex.Message}");
            return new PlanCallEvalReport.Row(
                model, cap, "error", null, 0, null, null, false, [], ex.Message);
        }
    }

    private static Plan? TryParse(string model, string text)
    {
        try
        {
            var plan = Parser().Parse(model, text);
            // A truncated response can still parse to an empty object — that is
            // the live failure mode, so an empty parse counts as not parsed.
            return plan.Steps.Count > 0 ? plan : null;
        }
        catch (ProviderException)
        {
            return null;
        }
    }

    private static bool CoversBothRepos(Plan plan) =>
        MentionsRepo(plan, ServerRepo) && MentionsRepo(plan, WorkerRepo);

    private static bool MentionsRepo(Plan plan, string repo) => plan.Steps.Any(s =>
        (s.TargetFile?.Value.Contains(repo, StringComparison.OrdinalIgnoreCase) ?? false)
        || s.Description.Contains(repo, StringComparison.OrdinalIgnoreCase));

    // ----- clients ---------------------------------------------------------

    private List<(string Model, IChatClient Client)> BuildClients()
    {
        var clients = new List<(string, IChatClient)>();
        var openAi = EvalChatClientEnv.TryBuild();
        if (openAi is not null) clients.Add((openAi.Value.ModelId, openAi.Value.Client));
        else output.WriteLine("SKIP OpenAI-family client: no AZURE_OPENAI_API_KEY / OPENAI_API_KEY.");

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            var model = Environment.GetEnvironmentVariable("AGENTSMITH_EVAL_CLAUDE_MODEL")
                ?? "claude-sonnet-5";
            var client = new ClaudeChatClientBuilder().Build(
                new AgentConfig { Type = "claude" }, new ModelAssignment { Model = model });
            clients.Add((model, client));
        }
        else output.WriteLine("SKIP Anthropic client: no ANTHROPIC_API_KEY.");
        return clients;
    }

    // ----- ticket + prompt material ---------------------------------------

    private static (string Source, Ticket Ticket) LoadTicket()
    {
        var envPath = Environment.GetEnvironmentVariable("AGENTSMITH_EVAL_TICKET_FILE");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return (Path.GetFileNameWithoutExtension(envPath),
                ToTicket(HtmlToText(File.ReadAllText(envPath))));

        var fixture = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "PlanGoldens", "synthetic-two-repo-migration.md");
        return ("synthetic-two-repo-migration", ToTicket(File.ReadAllText(fixture)));
    }

    private static Ticket ToTicket(string text)
    {
        var title = text.Split('\n')
            .Select(l => l.Trim().TrimStart('#').Trim())
            .FirstOrDefault(l => l.Length > 0) ?? "Two-repo library migration";
        if (title.Length > 120) title = title[..120];
        return new Ticket(new TicketId("eval-plan-golden"), title, text, null, "Open", "eval");
    }

    /// <summary>Strips markup from an HTML ticket export to plain text; plain
    /// text / markdown input passes through essentially unchanged.</summary>
    internal static string HtmlToText(string raw)
    {
        var text = Regex.Replace(raw, @"<(script|style)[^>]*>.*?</\1>", string.Empty,
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<(br|/p|/div|/li|/h[1-6]|/tr)[^>]*>", "\n",
            RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", string.Empty);
        return WebUtility.HtmlDecode(text).Trim();
    }

    // Two realistic repo code maps for the system prompt — the multi-repo
    // rules section only renders with more than one entry.
    private static Dictionary<string, string> CodeMaps() => new(StringComparer.Ordinal)
    {
        [ServerRepo] = """
            modules:
              - path: src/Sample.Server.Api            # HTTP endpoints, request DTOs
              - path: src/Sample.Server.Application    # command/query handlers (in-process mediator)
              - path: src/Sample.Server.Domain         # aggregates, domain events
              - path: src/Sample.Server.Infrastructure # persistence, broker publish adapters, outbox
            tests:
              - path: tests/Sample.Server.Tests
            """,
        [WorkerRepo] = """
            modules:
              - path: src/Sample.Worker                # hosted service, broker consumers
              - path: src/Sample.Worker.Application    # in-process command handlers
              - path: src/Sample.Worker.Infrastructure # broker wiring, retry configuration
            tests:
              - path: tests/Sample.Worker.Tests
            """,
    };

    private static Dictionary<string, ProjectMap> ProjectMaps() => new(StringComparer.Ordinal)
    {
        [ServerRepo] = new ProjectMap(
            "csharp",
            ["ASP.NET Core", "Entity Framework Core"],
            [
                new Module("src/Sample.Server.Api", ModuleRole.Production,
                    ["src/Sample.Server.Application"]),
                new Module("src/Sample.Server.Application", ModuleRole.Production,
                    ["src/Sample.Server.Domain"]),
                new Module("src/Sample.Server.Domain", ModuleRole.Production, []),
                new Module("src/Sample.Server.Infrastructure", ModuleRole.Production,
                    ["src/Sample.Server.Application", "src/Sample.Server.Domain"]),
            ],
            [new TestProject("tests/Sample.Server.Tests", "xunit", 42, null)],
            ["src/Sample.Server.Api/Program.cs"],
            new Conventions(null, null, null),
            new CiConfig(true, "dotnet build", "dotnet test", null)),
        [WorkerRepo] = new ProjectMap(
            "csharp",
            [".NET Worker Service"],
            [
                new Module("src/Sample.Worker", ModuleRole.Production,
                    ["src/Sample.Worker.Application"]),
                new Module("src/Sample.Worker.Application", ModuleRole.Production, []),
                new Module("src/Sample.Worker.Infrastructure", ModuleRole.Production,
                    ["src/Sample.Worker.Application"]),
            ],
            [new TestProject("tests/Sample.Worker.Tests", "xunit", 18, null)],
            ["src/Sample.Worker/Program.cs"],
            new Conventions(null, null, null),
            new CiConfig(true, "dotnet build", "dotnet test", null)),
    };

    // ----- plumbing --------------------------------------------------------

    private static PlanParser Parser() => new(new TolerantJsonParser(
        new NoOpTelemetry(), NullLogger<TolerantJsonParser>.Instance));

    private static EmbeddedPromptCatalog EmbeddedCatalog() => new(
        new EnvDirectoryPromptOverrideSource(NullLogger<EnvDirectoryPromptOverrideSource>.Instance),
        NullLogger<EmbeddedPromptCatalog>.Instance);

    // Same walk-up as the expectation golden: persist into the checkout, not
    // the bin dir, so the synthetic-fixture report is version-controlled.
    private static string ReportsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgentSmith.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the eval must run from a checkout to persist its report");
        return Path.Combine(dir!.FullName,
            "tests", "AgentSmith.PipelineHarness", "Reports", "plan-goldens");
    }

    private sealed class NoOpTelemetry : ITolerantParseTelemetry
    {
        public void Record(TolerantRecoveryKind kind, string detail) { }
    }
}
