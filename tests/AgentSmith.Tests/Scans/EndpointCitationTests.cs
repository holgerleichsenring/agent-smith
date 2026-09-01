using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// p0429a: the half p0429 named and could not build — a live-target finding cites an
/// ApiPath, not a file, so "read the file it names" resolved nothing and would have passed
/// every DAST finding through unchecked.
/// <para>
/// The rule is the same one, pointed at the evidence an api-scan actually holds: an
/// endpoint the loaded specification does not declare is invention, and one it does
/// declare is put to a refuter together with the request and the response that produced
/// it. A plausible copy of an exchange is not evidence.
/// </para>
/// </summary>
public sealed class EndpointCitationTests
{
    private const string RealPath = "/orders/{id}";

    [Fact]
    public async Task EndpointCitation_ForAPathTheSpecificationDoesNotContain_IsNotDelivered()
    {
        var invented = LiveFinding("/admin/backdoor", ObservationSeverity.Critical);

        var delivered = await Substantiate([invented], new ScriptedRefuter([]));

        delivered.Should().BeEmpty(
            "an endpoint the document never declared is the invented location, exactly as "
            + "an unreadable file path is");
    }

    [Fact]
    public async Task EndpointCitation_ForARealPath_ResolvesAndIsPutToTheRefuter()
    {
        var finding = LiveFinding("GET /orders/42", ObservationSeverity.Critical);
        var refuter = new ScriptedRefuter([]);

        var delivered = await Substantiate([finding], refuter);

        refuter.Asked.Should().ContainSingle(
            "a concrete call against a declared template is a real citation, not an invention");
        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.Critical);
    }

    [Fact]
    public async Task LiveTargetCandidate_CarriesTheExchangeNotAFileWindow()
    {
        var refuter = new ScriptedRefuter([]);

        await Substantiate([LiveFinding(RealPath, ObservationSeverity.High)], refuter);

        var candidate = refuter.Asked.Should().ContainSingle().Subject;
        candidate.Surface.Should().Be(EvidenceSurface.LiveTarget);
        candidate.Evidence.Should().Contain("GET /orders/{id}", "the declaration is shown");
        candidate.Evidence.Should().Contain("HTTP/1.1 200",
            "the refuter reads the response the scanner really got, not a summary of it");
        candidate.Evidence.Should().Contain("X-Api-Key: sample");
    }

    [Fact]
    public async Task RefuterQuotingTheRealResponse_DowngradesTheLiveTargetFinding()
    {
        var finding = LiveFinding(RealPath, ObservationSeverity.Critical);
        var refuter = new ScriptedRefuter(c =>
            [new FindingRefutation(RealPath, false, "HTTP/1.1 200 OK", "the endpoint answered normally", c[0].Id)]);

        var delivered = await Substantiate([finding], refuter);

        var kept = delivered.Should().ContainSingle().Subject;
        kept.Severity.Should().Be(ObservationSeverity.Medium);
        kept.Rationale.Should().Contain("the endpoint answered normally");
    }

    [Fact]
    public async Task LiveTargetFinding_WithNoSpecificationLoaded_ShipsUntouched()
    {
        var finding = LiveFinding("/orders/42", ObservationSeverity.Critical);
        var pipeline = ScanWith([finding]);
        var refuter = new ScriptedRefuter([]);

        var delivered = await Run(pipeline, refuter, spec: null);

        refuter.Asked.Should().BeEmpty();
        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.Critical,
            "a check with no evidence answers nothing — it must not refuse a healthy scan");
    }

    [Fact]
    public void CitedEndpointIndex_ToleratesTheWayAHumanWritesACitation()
    {
        var index = CitedEndpointIndex.FromSpec(Spec());

        index.Contains("GET /orders/{id}").Should().BeTrue();
        index.Contains("https://api.example.test/v1/orders/42?expand=true").Should().BeTrue(
            "a scanner reports a concrete URL against a template the document declares");
        index.Contains("/orders/42 (unauthenticated)").Should().BeTrue(
            "refusing a real endpoint over the parenthesis after it turns evidence into invention");
        index.Contains("OrderDto").Should().BeTrue("a schema the document declares is a citation too");
        index.Contains("/orders/42/refunds").Should().BeFalse();
        index.Contains("/admin/backdoor").Should().BeFalse();
    }

    [Fact]
    public void CitedEndpointIndex_WithNoSpecification_IsEmptyAndAnswersNothing()
    {
        CitedEndpointIndex.Empty.IsEmpty.Should().BeTrue();
        CitedEndpointIndex.Empty.Contains("/orders/42").Should().BeFalse();
    }

    private static Task<IReadOnlyList<SkillObservation>> Substantiate(
        List<SkillObservation> delivered, IFindingRefuter refuter) =>
        Run(ScanWith(delivered), refuter, Spec());

    private static async Task<IReadOnlyList<SkillObservation>> Run(
        PipelineContext pipeline, IFindingRefuter refuter, SwaggerSpec? spec)
    {
        if (spec is not null) pipeline.Set(ContextKeys.SwaggerSpecFull, spec);
        var substantiator = new FindingSubstantiator(
            new CandidateFindingFactory(
                new SourceCitationResolver(new CitedCodeWindow()),
                new EndpointCitationResolver(),
                NullLogger<CandidateFindingFactory>.Instance),
            refuter,
            new RefutationRouter(NullLogger<RefutationRouter>.Instance),
            new RefutationVerdicts(NullLogger<RefutationVerdicts>.Instance),
            new ScanEvidenceFactory(new ScannedSourceStub([])),
            NullLogger<FindingSubstantiator>.Instance);
        return await substantiator.SubstantiateAsync(pipeline, CancellationToken.None);
    }

    private static PipelineContext ScanWith(List<SkillObservation> delivered)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SkillObservations, delivered);
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("api-security-scan", new AgentConfig(), "skills", null));
        pipeline.Set(ContextKeys.ZapResult, new ZapResult([Alert()], 12, "baseline"));
        return pipeline;
    }

    /// <summary>The exchange ZAP really made — kept since p0429a instead of discarded.</summary>
    private static ZapFinding Alert() =>
        new("10038", "Missing header", "Medium", "High",
            "https://api.example.test/v1/orders/42", "desc", null, null, null, 1,
            new HttpExchange(
                "GET", "https://api.example.test/v1/orders/42",
                Request: "GET /v1/orders/42 HTTP/1.1\nHost: api.example.test\nX-Api-Key: sample",
                Response: "HTTP/1.1 200 OK\nContent-Type: application/json\n\n{\"id\":42}"));

    private static SwaggerSpec Spec() =>
        new("Sample API", "1.0",
            [new ApiEndpoint("get", RealPath, "getOrder", [], true, null, "OrderDto")],
            [], "{}");

    private static SkillObservation LiveFinding(string apiPath, ObservationSeverity severity) =>
        new(Id: 0, Role: "api-security-master", Concern: ObservationConcern.Security,
            Description: "the endpoint leaks data", Suggestion: "", Blocking: true,
            Severity: severity, Confidence: 80, ApiPath: apiPath);
}
