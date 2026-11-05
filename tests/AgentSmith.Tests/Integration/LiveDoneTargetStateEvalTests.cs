using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Demo;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Cli.Services.Demo;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Core.Services.Demo;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.Specs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-11-02-2b26c: the demo's two gpt-4.1 runs never reached coding — the attack and cut reviewers read
/// "the code does not apply the discount at 100.00 yet" as a refusal or a contradiction. This puts run 1's
/// draft before one attack reviewer and the rejected boundary wordings before the cut reviewer, over the
/// materialised demo sample and the demo's own ticket, three times each. Paid API; run by hand.
/// </summary>
[Trait("Category", "LiveLLM")]
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class LiveDoneTargetStateEvalTests(ITestOutputHelper output) : IDisposable
{
    private const string Repo = "source";
    private const int Runs = 3;
    private const string Boundary = "PriceCalculator.Total applies the bulk discount for totals of at least 100.00.";

    // Run 1's draft (2026-11-02-6e05a), its pointer to the carried .md left out.
    private const string RunOneDraft = """
        spec: 2026-11-02-6e05a
        goal: Bulk discount is applied to orders with Total > 100.00; change the logic so exactly 100.00 qualifies, matching the business rule and tests.
        repos:
        - source
        steps:
        - id: conditional-update
          action: Update PriceCalculator.Total logic to apply 10% discount for totals of 100.00 or more.
        - id: test-validate
          action: Verify Total_ExactlyAtBulkThreshold_GetsDiscount passes in PriceCalculatorTests.
        done:
        - PriceCalculator.Total applies the bulk discount for totals of at least 100.00.
        - tests/Sample.Tests/PriceCalculatorTests.cs passes Total_ExactlyAtBulkThreshold_GetsDiscount.
        assumptions:
        - PriceCalculator.Total currently applies the bulk discount only for totals strictly above 100.00.
        - Business rule and test require bulk discount for totals exactly at 100.00 as well.
        contexts:
        - backend
        """;

    // The wordings the cut reviews rejected: run.log line 3230, run2.log lines 2480-2481.
    private static readonly string[] Rejected =
    [
        "Bulk discount is applied to orders with Total >= 100.00 and the test passes.",
        "PriceCalculator.Total applies the bulk discount for totals >= 100.00.",
        "Orders with a total of exactly 100.00 are given the 10% bulk discount.",
        "PriceCalculator.Total applies the bulk discount to orders with total greater than or equal to 100.00.",
    ];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"agentsmith-2b26c-{Guid.NewGuid():N}");

    [Fact]
    public async Task SpecReview_AnOutcomeCriterionThatIsFalseAtBaseline_PassesUntouched()
    {
        if (!Credentials()) return;
        var draft = new SpecDraftReader().Read(RunOneDraft);
        var input = new SpecAttackInput(draft, "spec", 1, 0, SpecCutReviewTicketText.Of(Ticket()), true, [], 0, 0, [])
            { RepositoryNames = new Dictionary<string, string>(StringComparer.Ordinal) { [Repo] = Repo } };
        var reviewer = new SpecAttackReviewer(Factory(), new AsyncLocalRunContextAccessor(),
            new SpecAttackAdmission(NullLogger<SpecAttackAdmission>.Instance), NullLogger<SpecAttackReviewer>.Instance);
        var bad = new List<string>();
        for (var run = 1; run <= Runs; run++)
        {
            await using var look = await LookAsync(DerivationLookTerms.AttackReviewFor(draft));
            var result = await reviewer.ReviewAsync(input, look, new AgentConfig(), Cost(), CancellationToken.None);
            output.WriteLine($"attack run {run}: {result?.Verdict ?? "(none)"}");
            foreach (var cut in result?.Cuts ?? []) output.WriteLine($"  cut {cut.Register}: \"{cut.Quote}\" -> {cut.Replacement}");
            foreach (var finding in result?.Findings ?? []) output.WriteLine($"  finding: {finding.Text} [{finding.Evidence}]");
            if (result?.Verdict == "REFUSE") bad.Add($"run {run} refused");
            bad.AddRange((result?.Cuts ?? []).Where(c => Boundary.Contains(c.Quote, StringComparison.Ordinal))
                .Select(c => $"run {run} cut the boundary criterion: \"{c.Quote}\""));
        }
        bad.Should().BeEmpty("a done criterion the code does not meet yet is the work, not a fault");
    }

    [Fact]
    public async Task CutReview_TheRejectedBoundaryWordings_AreDeliverable()
    {
        if (!Credentials()) return;
        var draft = new SpecDraft("2026-11-02-5a85a", Rejected[1], $"spec: 2026-11-02-5a85a\ngoal: {Rejected[1]}", [])
            { Done = Rejected };
        var reviewer = new SpecCutReviewer(Factory(), new AsyncLocalRunContextAccessor(), NullLogger<SpecCutReviewer>.Instance);
        var bad = new List<string>();
        for (var run = 1; run <= Runs; run++)
        {
            await using var look = await LookAsync(DerivationLookTerms.CutReview);
            var review = await reviewer.ReviewAsync([draft], "inline", SpecCutReviewTicketText.Of(Ticket()), look,
                new AgentConfig(), Cost(), CancellationToken.None);
            output.WriteLine($"cut run {run}: {review.Findings.Count} finding(s){(review.Problem is null ? "" : $", {review.Problem}")}");
            foreach (var f in review.Findings) output.WriteLine($"  {f.Problem}: \"{f.Criterion}\" — {f.Why}");
            bad.AddRange(review.Findings.Where(f => Rejected.Contains(f.Criterion) || f.Criterion == draft.Goal)
                .Select(f => $"run {run} {f.Problem}: \"{f.Criterion}\""));
        }
        bad.Should().BeEmpty("the code not yet meeting a done entry is never a contradiction or a ticket gap");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best-effort cleanup */ }
    }

    private static AgentSmith.Domain.Entities.Ticket Ticket() => DemoTicket.Create().ToTicket();

    private static PipelineCostTracker Cost() => PipelineCostTracker.GetOrCreate(new PipelineContext());

    private static CutReviewTestDoubles.CappingFactory Factory() => new(LiveSkillProbeTests.BuildChatClient());

    private async Task<DerivationLook> LookAsync(DerivationLookTerms terms)
    {
        var workspace = Path.Combine(_dir, Guid.NewGuid().ToString("N"));
        await new DemoWorkspaceMaterializer(new EmbeddedDemoSample(),
                new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
                new LocalGitProcessInitializer(), NullLogger<DemoWorkspaceMaterializer>.Instance)
            .MaterializeAsync(workspace, CancellationToken.None);
        var sandbox = new InProcessSandbox("2b26c-eval", workspace, ownsWorkDir: false,
            InProcessStepEnvironment.Inherit, NullLogger.Instance);
        return new DerivationLook(new Dictionary<string, ISandbox> { [Repo] = sandbox }, new SandboxFileReaderFactory(),
            new PackageEcosystemDetector(), NullLogger.Instance, terms: terms);
    }

    private bool Credentials()
    {
        var have = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"))
                   || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        if (!have) output.WriteLine("SKIP: no AZURE_OPENAI_API_KEY / OPENAI_API_KEY in env — paid API, run by hand.");
        return have;
    }
}
