using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: the api scoreboard's own mechanics, proved with NO model, NO
/// credentials and NO docker — the declaration covers what is served, the two rates count
/// over their own populations, a finding naming a concrete request lands on its template,
/// a step that did not run is named beside the score, and the absence of an agent CLI is
/// loud.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ApiCorpusMechanicsTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ApiCorpus_DeclarationCoversEveryServedEndpoint()
    {
        var declaration = ApiTargetDeclarationLoader.Load(ApiTargetDeclarationLoader.DefaultPath);
        // Parsed by the PRODUCTION provider, so this compares the declaration against what
        // the scan really sees rather than against a second reading of the same file.
        var served = await new SwaggerProvider(NullLogger<SwaggerProvider>.Instance)
            .LoadAsync(FixturePaths.StubApiTargetOpenApi(), CancellationToken.None);

        var declared = declaration.Endpoints.Select(e => e.Describe()).ToList();
        var offered = served.Endpoints.Select(e => $"{e.Method.ToUpperInvariant()} {e.Path}").ToList();

        output.WriteLine($"served: {string.Join(", ", offered)}");
        offered.Should().BeEquivalentTo(declared,
            "an endpoint nobody declared is a finding with no denominator, and a declaration "
            + "nothing serves is a weakness the scan could never have found");
        declaration.Weak.Should().NotBeEmpty("there must be something to miss");
        declaration.Sound.Should().NotBeEmpty("there must be something to false-alarm on");
    }

    [Fact]
    public void ApiCorpus_AWeakEndpointWithNoFinding_CountsAsAMiss()
    {
        var report = ApiCorpusFixtures.ReportOf(ApiCorpusFixtures.OneOfEach());

        report.Misses.Should().Be(1);
        report.MissedEndpoints.Should().ContainSingle()
            .Which.Should().Be("GET " + ApiCorpusFixtures.WeakEndpoint);
        report.FalseAlarms.Should().Be(0);
    }

    [Fact]
    public void ApiCorpus_ASoundEndpointWithAFinding_CountsAsAFalseAlarm()
    {
        var report = ApiCorpusFixtures.ReportOf(
            ApiCorpusFixtures.OneOfEach(),
            ApiCorpusFixtures.FindingOn(ApiCorpusFixtures.SoundEndpoint));

        report.FalseAlarms.Should().Be(1, "the endpoint is sound and shaped to look otherwise");
        report.Misses.Should().Be(1, "and the real weakness was still not found");
        report.UndeclaredLocations.Should().BeEmpty();
    }

    [Fact]
    public void ApiCorpus_AFindingNamingTheConcreteRequest_MatchesItsPathTemplate()
    {
        var report = ApiCorpusFixtures.ReportOf(
            ApiCorpusFixtures.OneOfEach(),
            ApiCorpusFixtures.FindingOn("GET /members/42?expand=orders"));

        report.Detections.Should().Be(1,
            "naming the request it made and naming the route are a wording difference");
        report.UndeclaredLocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/members/42")]
    [InlineData("GET /members/42")]
    [InlineData("get /members/42")]
    [InlineData("http://127.0.0.1:5001/members/42")]
    [InlineData("/members/{id}")]
    [InlineData("/members/:id")]
    public void ApiCorpus_EveryWordingOfOneEndpoint_MatchesItsDeclaration(string location) =>
        ApiEndpointMatch.Matches(ApiCorpusFixtures.OneOfEach().Endpoints[0], location)
            .Should().BeTrue();

    [Theory]
    [InlineData("POST /members/42")]
    [InlineData("/members")]
    [InlineData("/members/42/role")]
    [InlineData("/orders/42")]
    [InlineData("")]
    public void ApiCorpus_ADifferentEndpoint_DoesNotMatch(string location) =>
        ApiEndpointMatch.Matches(ApiCorpusFixtures.OneOfEach().Endpoints[0], location)
            .Should().BeFalse(
                "segment counts must agree and a named method must be the declared one — "
                + "two declarations must never collapse into one match");

    [Fact]
    public void ApiCorpus_EachRate_UsesItsOwnDenominator()
    {
        var declaration = new ApiTargetDeclaration
        {
            Id = "denominators",
            Endpoints =
            [
                Weak("/a"), Weak("/b"), Weak("/c"), Weak("/d"), Sound("/e"),
            ],
        };

        var report = ApiCorpusFixtures.ReportOf(
            declaration,
            ApiCorpusFixtures.FindingOn("/a"),
            ApiCorpusFixtures.FindingOn("/e"));

        report.WeakPopulation.Should().Be(4);
        report.SoundPopulation.Should().Be(1);
        report.MissRate.Should().BeApproximately(0.75, 0.0001);
        report.FalseAlarmRate.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void ApiCorpus_AFindingNamingNothingDeclared_IsReportedWithoutADenominator()
    {
        var report = ApiCorpusFixtures.ReportOf(
            ApiCorpusFixtures.OneOfEach(), ApiCorpusFixtures.FindingOn("/invented/{id}"));

        report.FalseAlarms.Should().Be(0, "it named no sound endpoint either");
        report.UndeclaredLocations.Should().ContainSingle().Which.Should().Be("/invented/{id}");
    }

    [Fact]
    public void ApiCorpus_ACutOffStep_IsNamedBesideTheScore()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.NucleiResult, new NucleiResult(
            [], DurationSeconds: 600, RawOutput: string.Empty,
            Degraded: true, DegradedReason: "the step reached its 600s limit"));

        var named = ApiScanStepAccount.SilentSteps(pipeline, realScanners: true);

        named.Should().Contain(s => s.Contains("Nuclei") && s.Contains("600s limit"),
            "a step that reported completion at its time limit renders identically to a "
            + "clean target unless the score says otherwise");
        named.Should().Contain(s => s.StartsWith("Spectral", StringComparison.Ordinal)
            && s.Contains("did not run"));
        named.Should().Contain(s => s.StartsWith("ZAP", StringComparison.Ordinal)
            && s.Contains("did not run"));
    }

    [Fact]
    public void ApiCorpus_StubbedScanners_AreNamedAsContributingNothing()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.NucleiResult, new NucleiResult([], 1, string.Empty));

        var named = ApiScanStepAccount.SilentSteps(pipeline, realScanners: false);

        named.Should().Contain(s => s.Contains(ApiScanStepAccount.StubbedNote),
            "a tier that stubs its dynamic scanners must never let a score read as a "
            + "measurement of a scan half of which never executed");
    }

    [Fact]
    public void ApiCorpus_NoAgentCliConfigured_SkipsAndSaysSo()
    {
        const string absent = "agentsmith-no-such-agent-cli";

        AgentCliProbe.IsAvailable(absent).Should().BeFalse();
        AgentCliProbe.SkipReason(absent).Should()
            .Contain(absent).And.Contain("NOTHING WAS MEASURED");
    }

    [Fact]
    public void ApiCorpus_TheReport_StatesWhatItCannotGrade()
    {
        var markdown = ApiCorpusReportWriter.RenderMarkdown(
            ApiCorpusFixtures.ReportOf(ApiCorpusFixtures.OneOfEach()));

        markdown.Should().Contain(ApiCorpusReport.CannotGradeSentence);
        markdown.Should().Contain("Every step contributed.");
    }

    /// <summary>
    /// The seam the spec called out: LoadSwagger resolves through ISwaggerProvider, and the
    /// harness's stub answers ONE invented endpoint whatever it is asked. Under it the
    /// served document would be decorative and the score would be of a fiction.
    /// </summary>
    [Fact]
    public async Task ApiCorpus_TheEvalComposition_ReadsTheServedDocumentAndNotAStub()
    {
        await using var target = await StubApiTargetHost.StartAsync(
            FixturePaths.StubApiTargetOpenApi());
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), SandboxBackend.Stub, session: null,
            SkillsBackend.Stub, ScanEvalComposition.DrivenByAgentCliAgainstAServedTarget());

        var provider = harness.Services.GetRequiredService<ISwaggerProvider>();
        var spec = await provider.LoadAsync(target.LoopbackOpenApiUrl, CancellationToken.None);

        output.WriteLine($"{spec.Title}: {spec.Endpoints.Count} endpoints over HTTP");
        spec.Title.Should().Be("Reference Target API",
            "the document that reaches the master is the one the target serves");
        spec.Endpoints.Should().HaveCount(
            ApiTargetDeclarationLoader.Load(ApiTargetDeclarationLoader.DefaultPath).Endpoints.Count);
    }

    /// <summary>The served BEHAVIOUR, which is what a probing master actually meets. The
    /// declared weaknesses are in the responses, not only in the document.</summary>
    [Fact]
    public async Task ApiCorpus_TheServedTarget_BehavesAsItsDeclarationSays()
    {
        await using var target = await StubApiTargetHost.StartAsync(
            FixturePaths.StubApiTargetOpenApi());
        using var http = new HttpClient { BaseAddress = new Uri(target.LoopbackUrl) };

        var unauthenticated = await http.GetAsync("/members/7", CancellationToken.None);
        unauthenticated.IsSuccessStatusCode.Should().BeTrue(
            "GET /members/{id} is declared weak because it answers with no authorization at all");

        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "member-1");

        var unscoped = await http.GetAsync("/orders?memberId=member-2", CancellationToken.None);
        (await unscoped.Content.ReadAsStringAsync(CancellationToken.None))
            .Should().Contain("member-2",
                "GET /orders is declared weak because it never checks memberId against the bearer");

        var verbose = await http.PostAsync(
            "/invoices", new StringContent("{}"), CancellationToken.None);
        (await verbose.Content.ReadAsStringAsync(CancellationToken.None))
            .Should().Contain("InvoiceRepository",
                "POST /invoices is declared weak because its error says too much");

        var scoped = await http.GetAsync("/orders/order-member-2-1", CancellationToken.None);
        scoped.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound,
            "GET /orders/{id} is declared sound because it scopes to the bearer");
    }

    private static ApiEndpointDeclaration Weak(string path) => new()
    {
        Method = "GET", Path = path, Verdict = ApiTargetDeclaration.Verdicts.Weak,
        Class = "missing-authorization",
    };

    private static ApiEndpointDeclaration Sound(string path) => new()
    {
        Method = "GET", Path = path, Verdict = ApiTargetDeclaration.Verdicts.Sound,
        Class = "missing-authorization",
    };
}
