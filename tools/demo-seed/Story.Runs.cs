using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Verify;
using AgentSmith.Infrastructure.Persistence.Services;

namespace DemoSeed;

internal static partial class Story
{
    private const string Sonnet = "claude-sonnet-4-5";
    private const string Haiku = "claude-haiku-4-5";

    // The story is anchored a few hours before the seed runs, so the dashboard's relative times read naturally.
    public static readonly DateTimeOffset Anchor =
        new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, DateTimeOffset.UtcNow.Day,
            DateTimeOffset.UtcNow.Hour, 0, 0, TimeSpan.Zero).AddHours(-5);

    public static string RunIdAt(DateTimeOffset at, string suffix) =>
        $"{at.UtcDateTime:yyyy-MM-ddTHH-mm-ss}-{suffix}";

    public static string MarkerRunId => RunIdAt(Anchor.AddMinutes(20), "d41a");

    public const string SpecA = "2026-11-18-7b2ea";
    public const string SpecB = "2026-11-18-7b2eb";

    public static readonly string[] CodeSteps =
    [
        "LoadCatalogCommand", "FetchTicketCommand", "ScopeReposCommand", "CheckoutSourceCommand",
        "RunPreflightCommand", "LoadCodingPrinciplesCommand", "LoadContextCommand", "AnalyzeCodeCommand",
        "SpecGateCommand", "AgenticExecuteCommand", "VerifySpecCommand", "AgenticExecuteCommand",
        "VerifySpecCommand", "WriteRunResultCommand", "CommitAndPRCommand", "CompletePullRequestsCommand",
    ];

    public static async Task RunsAsync(RunDbProjector projector, CancellationToken ct)
    {
        await VolumeDiscountRunAsync(projector, ct);
        await CiFixRunAsync(projector, ct);
        await ShortRunAsync(projector, "44", "Saved for later: keep cart items across sessions", Anchor.AddMinutes(150), "e44b",
            SavedSpecs, finished: true, pr: "https://github.com/acme/shop-api/pull/58", cost: 1.84m, ct);
        await ShortRunAsync(projector, "45", "Reorder: one click from the order history", Anchor.AddMinutes(232), "e45c",
            ReorderSpecs, finished: false, pr: null, cost: 0.62m, ct);
        await ShortRunAsync(projector, "39", "Search: ignore accents in product names", Anchor.AddMinutes(-160), "c39a",
            [new("2026-11-18-3a91", "search-accent-folding", "Product search matches names regardless of accents.", "shop-api", ["fold accents in the search index"], ["'creme' finds 'Crème brûlée'"])],
            finished: true, pr: "https://github.com/acme/shop-api/pull/55", cost: 0.71m, ct);
    }

    public static string CiFixRunId => RunIdAt(Anchor.AddMinutes(36), "d41b");

    /// <summary>#41, attempt 2: CI went red on the pull request; the CI loop started a fix run with the failing check.</summary>
    private static async Task CiFixRunAsync(RunDbProjector projector, CancellationToken ct)
    {
        const string pr = "https://github.com/acme/shop-api/pull/57";
        string[] steps = ["FetchTicketCommand", "CheckoutSourceCommand", "AgenticExecuteCommand", "VerifySpecCommand", "CommitAndPRCommand", "CompletePullRequestsCommand"];
        var run = new RunScript(projector, CiFixRunId, Anchor.AddMinutes(36));
        run.Start("shop", "code", "41", ["shop-api"], steps, trigger: "ci")
           .Add(new TicketFetchedEvent(run.RunId, "41", "Checkout: apply volume discounts to the cart total",
               "Orders of 10 or more units of one product get the tier discount from the price list.", "open", ["shop"], 0, "github", run.Tick(2),
               Cause: new AgentSmith.Contracts.Runs.AttemptCause("ci", "CI", Anchor.AddMinutes(35),
                   new AgentSmith.Contracts.Runs.AttemptSource("pull_request", pr, "57", "check 'build / format' failed")),
               WebUrl: "https://github.com/acme/shop/issues/41"))
           .Step("FetchTicketCommand", "Read the ticket", seconds: 1, body: b => b
               .Detail("ci", "CI on acme/shop-api#57: 'build / format' failed — src/Shop.Api/Pricing/VolumeTier.cs: whitespace formatting (IDE0055)."))
           .Step("CheckoutSourceCommand", "Check out", seconds: 4, body: b => b
               .Tool("git", "fetch agent-smith/41 at the pull request's head", repo: "shop-api", role: null, seconds: 2.4))
           .Step("AgenticExecuteCommand", "Fix the failing check", SpecA, seconds: 38, body: b => b
               .Llm("primary", Sonnet, 22_400, 640, 0.0768m, 8.4, cached: 18_000, repo: "shop-api")
               .Tool("run_command", "dotnet format --verify-no-changes", ok: false, repo: "shop-api", error: "VolumeTier.cs(14,1): IDE0055", seconds: 6.2)
               .Tool("run_command", "dotnet format", repo: "shop-api", seconds: 5.8)
               .Llm("primary", Sonnet, 23_900, 210, 0.0748m, 3.9, cached: 22_000, repo: "shop-api"))
           .Step("VerifySpecCommand", "Build and test", SpecA, seconds: 46, body: b => b
               .Raw(new VerifyPassRecordedEvent(run.RunId, run.Tick(0.5), "passed",
                   [new VerifyRepositoryPass("shop-api", "passed",
                       [new VerifyStageRun("format", "dotnet format --verify-no-changes", 0, false), new VerifyStageRun("build", "dotnet build", 0, false), new VerifyStageRun("test", "dotnet test", 0, false)])], [])))
           .Step("CommitAndPRCommand", "Push to the pull request", seconds: 4, body: b => b
               .Tool("git", "commit + push agent-smith/41 (format)", repo: "shop-api", role: null, seconds: 2.0)
               .Raw(new PullRequestOutcomeEvent(run.RunId, "shop-api", "opened", run.Tick(1), pr)))
           .Step("CompletePullRequestsCommand", "Complete pull requests", seconds: 2, body: b => b
               .Detail("merge", "merge.when on_ci_green: waits for CI on the new head, then merges."))
           .Finish("success", "Fixed the failing 'build / format' check; CI green on the new head and the pull request merged.", 0.1516m, pr);
        await run.WriteAsync(ct);
    }

    /// <summary>A shorter code run over a ticket's approved specs; unfinished runs stop mid-spec.</summary>
    private static async Task ShortRunAsync(RunDbProjector projector, string ticket, string title, DateTimeOffset start,
        string suffix, DemoSpec[] specs, bool finished, string? pr, decimal cost, CancellationToken ct)
    {
        var steps = new List<string> { "LoadCatalogCommand", "FetchTicketCommand", "CheckoutSourceCommand", "AnalyzeCodeCommand", "SpecGateCommand" };
        foreach (var _ in specs) steps.AddRange(["AgenticExecuteCommand", "VerifySpecCommand"]);
        steps.AddRange(["WriteRunResultCommand", "CommitAndPRCommand", "CompletePullRequestsCommand"]);
        var run = new RunScript(projector, RunIdAt(start, suffix), start);
        var perCall = cost / (3 + specs.Length * 3);
        run.Start("shop", "code", ticket, [.. specs.Select(x => x.Repo).Distinct()], steps)
           .Ticket(ticket, title, specs[0].Goal, string.Join("\n", specs.SelectMany(x => x.Done).Select((d, i) => $"{i + 1}. {d}")),
               $"https://github.com/acme/shop/issues/{ticket}")
           .Step("LoadCatalogCommand", "Load skills", seconds: 1)
           .Step("FetchTicketCommand", "Read the ticket", seconds: 1)
           .Step("CheckoutSourceCommand", "Check out", seconds: 5, body: b => b
               .Tool("git", $"clone, branch agent-smith/{ticket}", repo: specs[0].Repo, role: null, seconds: 3.6))
           .Step("AnalyzeCodeCommand", "Analyze codebase", seconds: 20, body: b => b
               .Llm("scout", Haiku, 16_000, 1_100, perCall, 6.0, cached: 8_000, repo: specs[0].Repo)
               .Llm("scout", Haiku, 21_000, 1_700, perCall, 7.0, cached: 15_000, repo: specs[0].Repo))
           .Step("SpecGateCommand", "Check the specs", seconds: 1, body: b => b
               .Raw(new SpecSetPublishedEvent(run.RunId, run.Tick(0.2), RevisionNumber: 1, RevisionCause: "initial", SeriesId: "2026-11-18-9c4f",
                   ApprovalAt: Anchor.AddMinutes(105), ApprovalConversation: SeriesConversation, ApprovalPrincipal: "Sam Sample", Committed: true,
                   Specs: [.. specs.Select((x, i) => new SpecCutEntry(x.Id, i + 1, $"{x.Id}-{x.Label}", x.Goal, [x.Repo]))])));
        for (var i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            var last = !finished && i == specs.Length - 1;
            run.Step("AgenticExecuteCommand", spec.Label, spec.Id, seconds: 90, body: b =>
            {
                b.Raw(new SpecStateChangedEvent(run.RunId, spec.Id, i + 1, spec.Goal, SpecRunState.InProgress, null, run.Tick(0.1)))
                 .Llm("primary", Sonnet, 30_000, 1_500, perCall, 12.0, cached: 20_000, repo: spec.Repo)
                 .Tool("read_file", spec.Steps[0], repo: spec.Repo)
                 .Llm("primary", Sonnet, 34_000, 1_900, perCall, 14.0, cached: 28_000, repo: spec.Repo);
                if (!last) b.Llm("primary", Sonnet, 36_000, 800, perCall, 8.0, cached: 33_000, repo: spec.Repo);
            }, status: last ? "running" : "success");
            if (last) break;
            run.Step("VerifySpecCommand", "Build and test", spec.Id, seconds: 40, body: b => b
                .Raw(new SpecStateChangedEvent(run.RunId, spec.Id, i + 1, spec.Goal, SpecRunState.Done, "passed", run.Tick(0.2),
                    AcceptanceJson([.. spec.Done.Select(d => (d, "met", "covered by the spec's tests", (string?)null))]))));
        }
        if (finished)
        {
            run.Step("WriteRunResultCommand", "Write the result", seconds: 5, body: b => b.Llm("summarization", Haiku, 9_000, 700, perCall, 4.0))
               .Step("CommitAndPRCommand", "Commit and open the pull request", seconds: 6, body: b => b
                   .Raw(new PullRequestOutcomeEvent(run.RunId, specs[0].Repo, "opened", run.Tick(1), pr)))
               .Step("CompletePullRequestsCommand", "Complete pull requests", seconds: 2)
               .Finish("success", $"{specs.Length} spec(s) built and verified; pull request opened.", cost, pr);
        }
        await run.WriteAsync(ct);
    }

    /// <summary>#41: a finished coding run over two approved specs, one pull request, CI green.</summary>
    private static async Task VolumeDiscountRunAsync(RunDbProjector projector, CancellationToken ct)
    {
        var run = new RunScript(projector, MarkerRunId, Anchor.AddMinutes(20));
        run.Start("shop", "code", "41", ["shop-api"], CodeSteps)
           .Ticket("41", "Checkout: apply volume discounts to the cart total",
               "Orders of 10 or more units of one product get the tier discount from the price list. "
               + "The cart shows the discount as its own line.",
               "1. A cart line with 10+ units is priced at the tier rate.\n2. The cart total shows the discount as a separate line.\n3. Existing coupon codes still apply after the tier discount.",
               "https://github.com/acme/shop/issues/41")
           .Step("LoadCatalogCommand", "Load skills", seconds: 1)
           .Step("FetchTicketCommand", "Read the ticket", seconds: 1)
           .Step("ScopeReposCommand", "Pick repositories", seconds: 3, body: b => b
               .Llm("scout", Haiku, 3_200, 180, 0.0041m, 2.1)
               .Decision("scope", "shop-api", "shop-web", "The discount is computed server-side; the cart page already renders whatever lines the API returns."))
           .Step("CheckoutSourceCommand", "Check out", seconds: 6, body: b => b
               .Tool("git", "clone acme/shop-api, branch agent-smith/41-volume-discounts", repo: "shop-api", role: null, seconds: 4.2))
           .Step("RunPreflightCommand", "Preflight", seconds: 9, body: b => b
               .Tool("run_command", "dotnet build", repo: "shop-api", seconds: 8.1))
           .Step("LoadCodingPrinciplesCommand", "Load principles", seconds: 1)
           .Step("LoadContextCommand", "Load context", seconds: 1)
           .Step("AnalyzeCodeCommand", "Analyze codebase", seconds: 24, body: b => b
               .Llm("scout", Haiku, 18_400, 1_250, 0.0247m, 6.3, cached: 9_000, repo: "shop-api")
               .Tool("list_files", "src/Shop.Api/Pricing", repo: "shop-api", role: "scout")
               .Tool("read_file", "src/Shop.Api/Pricing/PriceCalculator.cs", repo: "shop-api", role: "scout")
               .Llm("scout", Haiku, 24_900, 2_100, 0.0353m, 8.8, cached: 18_000, repo: "shop-api"))
           .Step("SpecGateCommand", "Check the specs", seconds: 1, body: b => b
               .Detail("gate", "2 approved specs read from agent-smith/41-volume-discounts; nothing derived again.")
               .Raw(new SpecSetPublishedEvent(run.RunId, run.Tick(0.2), RevisionNumber: 1, RevisionCause: "initial", SeriesId: "2026-11-18-7b2e",
                   ApprovalAt: Anchor.AddMinutes(-30), ApprovalConversation: Conversation, ApprovalPrincipal: "Sam Sample", Committed: true,
                   Specs: [.. VolumeSpecs.Select((x, i) => new SpecCutEntry(x.Id, i + 1, $"{x.Id}-{x.Label}", x.Goal, [x.Repo]))]))
               .Raw(new SpecRecordedEvent(run.RunId, SpecA, SpecYaml(VolumeSpecs[0], "41"), run.Tick(0.1)))
               .Raw(new SpecRecordedEvent(run.RunId, SpecB, SpecYaml(VolumeSpecs[1], "41"), run.Tick(0.1))))
           .Step("AgenticExecuteCommand", "Price tiers", SpecA, seconds: 140, body: b => b
               .Raw(new SpecStateChangedEvent(run.RunId, SpecA, 1, VolumeSpecs[0].Goal, SpecRunState.InProgress, null, run.Tick(0.1)))
               .Llm("primary", Sonnet, 31_200, 1_840, 0.1212m, 14.2, cached: 22_000, repo: "shop-api")
               .Tool("read_file", "src/Shop.Api/Pricing/PriceCalculator.cs", repo: "shop-api")
               .Tool("read_file", "src/Shop.Api/Pricing/PriceList.cs", repo: "shop-api")
               .Llm("primary", Sonnet, 36_800, 2_420, 0.1467m, 17.9, cached: 31_000, repo: "shop-api")
               .Decision("design", "Tier lookup in PriceList", "a discount table in the cart",
                   "PriceList already owns per-product prices; a second table would drift from it.")
               .Tool("write_file", "src/Shop.Api/Pricing/VolumeTier.cs", repo: "shop-api")
               .Tool("write_file", "src/Shop.Api/Pricing/PriceCalculator.cs", repo: "shop-api")
               .Llm("primary", Sonnet, 41_500, 1_960, 0.1543m, 15.1, cached: 36_000, repo: "shop-api")
               .Tool("write_file", "tests/Shop.Api.Tests/Pricing/VolumeTierTests.cs", repo: "shop-api")
               .Tool("run_command", "dotnet test --filter VolumeTier", repo: "shop-api", seconds: 21.4)
               .Llm("primary", Sonnet, 44_100, 720, 0.1424m, 7.8, cached: 41_000, repo: "shop-api"))
           .Step("VerifySpecCommand", "Build and test", SpecA, seconds: 48, body: b => b
               .Tool("run_command", "dotnet build && dotnet test", repo: "shop-api", role: null, seconds: 44)
               .Raw(new VerifyPassRecordedEvent(run.RunId, run.Tick(0.5), "passed",
                   [new VerifyRepositoryPass("shop-api", "passed",
                       [new VerifyStageRun("build", "dotnet build", 0, false), new VerifyStageRun("test", "dotnet test", 0, false)])], []))
               .Raw(new SpecStateChangedEvent(run.RunId, SpecA, 1, VolumeSpecs[0].Goal, SpecRunState.Done, "passed", run.Tick(0.2),
                   AcceptanceJson(("a 10-unit line is priced at the tier rate", "met", "VolumeTierTests covers 9, 10 and 50 units", "tests/Shop.Api.Tests/Pricing/VolumeTierTests.cs"),
                                  ("dotnet test passes", "met", "142 passed, 0 failed", null)))))
           // 2026-11-18-c8e2d: the master leads — it briefs a builder, looks at its work, sends it back once, and reports.
           .Step("AgenticExecuteCommand", "Discount line in the cart total", SpecB, seconds: 96, body: b => b
               .Raw(new SpecStateChangedEvent(run.RunId, SpecB, 2, VolumeSpecs[1].Goal, SpecRunState.InProgress, null, run.Tick(0.1)))
               .Llm("primary", Sonnet, 38_900, 1_610, 0.1301m, 12.6, cached: 33_000, repo: "shop-api")
               .Tool("read_file", "src/Shop.Api/Cart/CartTotals.cs", repo: "shop-api")
               .Raw(new SubAgentSpawnedEvent(run.RunId, "sa-cart1", "CartBuilder", "build the discount line in the cart total",
                   null, "sha256:0c1d2e3f4a5b", run.Tick(0.4)))
               .Tool("write_file", "src/Shop.Api/Cart/CartTotals.cs", repo: "shop-api", role: "CartBuilder")
               .Llm("CartBuilder", Sonnet, 42_300, 1_380, 0.1356m, 11.2, cached: 38_000, repo: "shop-api")
               .Tool("run_command", "dotnet test --filter CartTotals", repo: "shop-api", role: "CartBuilder", seconds: 18.0)
               .Raw(new SubAgentCompletedEvent(run.RunId, "sa-cart1", "Succeeded", 3, 0, 0, 4, 0.1356m, run.Tick(0.5)))
               .Tool("read_file", "src/Shop.Api/Cart/CartTotals.cs", repo: "shop-api")
               .Raw(new SubAgentMessagedEvent(run.RunId, "sa-cart1",
                   "Acceptance 3: a coupon still applies after the tier discount, and nothing tests that order. Add a test for it.",
                   true, run.Tick(0.3)))
               .Decision("order", "Tier discount before coupons", "coupons first",
                   "Acceptance 3 says coupons still apply after the tier discount.")
               .Tool("write_file", "tests/Shop.Api.Tests/Cart/CartTotalsTests.cs", repo: "shop-api", role: "CartBuilder")
               .Raw(new SubAgentCompletedEvent(run.RunId, "sa-cart1", "Succeeded", 2, 0, 0, 2, 0.0412m, run.Tick(0.5)))
               .Tool("run_command", "dotnet test --filter CartTotals", repo: "shop-api", seconds: 18.0)
               .Llm("primary", Sonnet, 45_000, 540, 0.1395m, 6.1, cached: 42_000, repo: "shop-api"))
           .Step("VerifySpecCommand", "Build and test", SpecB, seconds: 51, body: b => b
               .Tool("run_command", "dotnet build && dotnet test", repo: "shop-api", role: null, seconds: 47)
               .Raw(new VerifyPassRecordedEvent(run.RunId, run.Tick(0.5), "passed",
                   [new VerifyRepositoryPass("shop-api", "passed",
                       [new VerifyStageRun("build", "dotnet build", 0, false), new VerifyStageRun("test", "dotnet test", 0, false)])], []))
               .Raw(new SpecStateChangedEvent(run.RunId, SpecB, 2, VolumeSpecs[1].Goal, SpecRunState.Done, "passed", run.Tick(0.2),
                   ReportJson([("the cart total response carries a 'Volume discount' line", "met", "CartTotalsTests asserts the line and its amount", "tests/Shop.Api.Tests/Cart/CartTotalsTests.cs"),
                               ("a coupon still applies after the tier discount", "met", "tier first, then coupon; covered by CartTotalsTests.TierThenCoupon", null)],
                       ["CartTotals rounded the line total before the tier was applied; it rounds once, at the end"],
                       [new FollowUpView("The order confirmation email shows the volume discount line",
                           "the email template lives in shop-web and is its own change", SpecB)]))))
           .Step("WriteRunResultCommand", "Write the result", seconds: 6, body: b => b
               .Llm("summarization", Haiku, 12_300, 900, 0.0168m, 5.2)
               .Raw(new RunStoryRecordedEvent(run.RunId,
                   LedgerJson(("Add VolumeTier and the tier lookup", "done", "src/Shop.Api/Pricing/VolumeTier.cs; src/Shop.Api/Pricing/PriceList.cs"),
                              ("Apply the tier in PriceCalculator", "done", "src/Shop.Api/Pricing/PriceCalculator.cs"),
                              ("Return the discount line from CartTotals", "done", "src/Shop.Api/Cart/CartTotals.cs"),
                              ("Tests for tiers, discount line and coupons", "done", "tests/Shop.Api.Tests/Pricing/VolumeTierTests.cs; tests/Shop.Api.Tests/Cart/CartTotalsTests.cs")),
                   ReportJson([("A cart line with 10+ units is priced at the tier rate.", "met", "VolumeTierTests", null),
                               ("The cart total shows the discount as a separate line.", "met", "CartTotalsTests", null),
                               ("Existing coupon codes still apply after the tier discount.", "met", "CartTotalsTests.TierThenCoupon", null)],
                       ["CartTotals rounded the line total before the tier was applied; it rounds once, at the end"],
                       [new FollowUpView("The order confirmation email shows the volume discount line",
                           "the email template lives in shop-web and is its own change", SpecB)]),
                   run.Tick(0.2))))
           .Step("CommitAndPRCommand", "Commit and open the pull request", seconds: 7, body: b => b
               .Tool("git", "commit + push agent-smith/41-volume-discounts", repo: "shop-api", role: null, seconds: 3.1)
               .Raw(new PullRequestOutcomeEvent(run.RunId, "shop-api", "opened", run.Tick(1),
                   "https://github.com/acme/shop-api/pull/57")))
           .Step("CompletePullRequestsCommand", "Complete pull requests", seconds: 2)
           .Finish("success", "Volume tiers priced in PriceList; the cart total carries a separate discount line. 2 specs met, pull request opened.",
               1.2307m, "https://github.com/acme/shop-api/pull/57");
        await run.WriteAsync(ct);
    }
}
