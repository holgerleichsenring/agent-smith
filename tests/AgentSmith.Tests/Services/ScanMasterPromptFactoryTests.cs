using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0278: the scan master's review user prompt carries the scanner findings (+ the
/// OpenAPI spec for api-security) inline and frames the run as a read-only review that
/// emits an observation array — never a code change.
/// </summary>
public sealed class ScanMasterPromptFactoryTests
{
    private static readonly Repository Repo = new(new BranchName("main"), "https://example.test/repo.git");
    private readonly ScanMasterPromptFactory _sut = new();

    [Fact]
    public void ScanMasterPromptFactory_ApiContext_IncludesNucleiZapSummaryAndSpec()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.NucleiResult, new NucleiResult(
            [new NucleiFinding("t1", "SQLi", "high", "https://x/orders", null, null)], 10, ""));
        pipeline.Set(ContextKeys.ZapResult, new ZapResult(
            [new ZapFinding("1", "Permissive CORS", "Low", "High", "https://x", "desc", null, null, null, 1)],
            10, "api-scan", 2));
        pipeline.Set(ContextKeys.SwaggerSpec, "OPENAPI_SPEC_BODY_MARKER");

        var prompt = _sut.Build(pipeline, Repo, ["repo"]);

        prompt.Should().Contain("### Nuclei");
        prompt.Should().Contain("### ZAP");
        prompt.Should().Contain("OpenAPI spec");
        prompt.Should().Contain("OPENAPI_SPEC_BODY_MARKER");
    }

    [Fact]
    public void ScanMasterPromptFactory_SecurityContext_FormatsRawSkillObservations()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SkillObservations, new List<SkillObservation>
        {
            new(Id: 0, Role: "static-pattern-scanner", Concern: ObservationConcern.Security,
                Description: "hardcoded AWS secret", Suggestion: "", Blocking: false,
                Severity: ObservationSeverity.High, Confidence: 90, File: "src/Config.cs", StartLine: 12,
                EvidenceMode: EvidenceMode.AnalyzedFromSource, Category: "secrets"),
        });

        var prompt = _sut.Build(pipeline, Repo, ["repo"]);

        prompt.Should().Contain("Scanner Findings");
        prompt.Should().Contain("static-pattern-scanner");
        prompt.Should().Contain("hardcoded AWS secret");
        prompt.Should().NotContain("### Nuclei", "no api scanner results in a code-security context");
    }

    [Fact]
    public void ScanMasterPromptFactory_AlwaysForbidsCodeChangesAndBuild()
    {
        var prompt = _sut.Build(new PipelineContext(), Repo, ["repo"]);

        prompt.Should().Contain("SECURITY REVIEW");
        prompt.Should().Contain("NOT modify");
        prompt.Should().Contain("NOT run a build");
        prompt.Should().Contain("observation array");
    }
}
