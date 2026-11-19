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
/// rejected boundary wordings before the cut reviewer, over the materialised demo sample and the demo's own ticket,
/// three times. Paid API; run by hand. 2026-11-19-3e10: the attack reviewer is gone, and its run with it.
/// </summary>
[Trait("Category", "LiveLLM")]
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class LiveDoneTargetStateEvalTests(ITestOutputHelper output) : IDisposable
{
    private const string Repo = "source";
    private const int Runs = 3;

    // Run 1's draft (2026-11-02-6e05a), its pointer to the carried .md left out.

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
            var review = await reviewer.ReviewAsync([draft], "inline", Ticket().Description, look,
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

    private static CutReviewTestDoubles.CappingFactory Factory() => new(LiveChatClient.Build());

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
