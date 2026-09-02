using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// p0429: the half that answers the measured damage — a security scan that delivered
/// NINE false-positive CRITICALs, every one of them promoted because the scan master
/// said nothing about it.
/// <para>
/// The rule these tests pin is symmetric: silence never decides. Master silence no
/// longer promotes a finding to delivered-and-critical, and refuter silence never
/// demotes one. Only a rebuttal QUOTING the code it was shown moves a finding, and only
/// downward — the goal is not fewer findings.
/// </para>
/// </summary>
public sealed class FindingSubstantiationTests
{
    private const string SecretLine = "    var apiKey = \"AKIA0000EXAMPLE\";";
    private const string RealFile = "src/Config.cs";

    [Fact]
    public async Task Substantiation_FindingCitingNoScannedFile_IsNotDelivered()
    {
        // The mechanical half: a citation resolving against nothing the scan can read is
        // a fabrication, and no model is asked about it.
        var invented = Scanner(ObservationSeverity.Critical, "src/Imagined.cs", 4);
        var pipeline = ScanWith([invented], [invented]);

        var delivered = await Substantiate(pipeline, new ScriptedRefuter([]));

        delivered.Should().BeEmpty("a finding pointing at a file the scan never saw is invention");
    }

    [Fact]
    public async Task Substantiation_RefutedCritical_ShipsDowngradedNotCritical()
    {
        var finding = Scanner(ObservationSeverity.Critical, RealFile, 2);
        var pipeline = ScanWith([finding], [finding]);
        var refuter = new ScriptedRefuter(c =>
            [new FindingRefutation($"{RealFile}:2", false, SecretLine.Trim(), "an example value in a sample file", c[0].Id)]);

        var delivered = await Substantiate(pipeline, refuter);

        var kept = delivered.Should().ContainSingle().Subject;
        kept.Severity.Should().Be(ObservationSeverity.Medium,
            "a finding nobody could substantiate must not reach a reviewer as critical");
        kept.ReviewStatus.Should().Be(RefutedFinding.ReviewStatus);
        kept.Rationale.Should().Contain("an example value in a sample file",
            "the reviewer reads WHY in one line instead of working it out again");
    }

    [Fact]
    public async Task Substantiation_RefuterQuotesCodeItWasNeverShown_FindingStands()
    {
        var finding = Scanner(ObservationSeverity.Critical, RealFile, 2);
        var pipeline = ScanWith([finding], [finding]);
        var refuter = new ScriptedRefuter(c =>
            [new FindingRefutation($"{RealFile}:2", false, "this line is nowhere in the file", "trust me", c[0].Id)]);

        var delivered = await Substantiate(pipeline, refuter);

        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.Critical,
            "a refuter that invents its objection would silence a real critical — "
            + "the unquoted rebuttal is discarded and the finding stands");
    }

    [Fact]
    public async Task Substantiation_RefuterUnreachable_FindingShipsUnchanged()
    {
        var finding = Scanner(ObservationSeverity.Critical, RealFile, 2);
        var pipeline = ScanWith([finding], [finding]);

        var delivered = await Substantiate(pipeline, ScriptedRefuter.Unreachable());

        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.Critical,
            "silence is not a verdict in either direction — a call that failed leaves the scan as loud as it was");
    }

    [Fact]
    public async Task Substantiation_SubstantiatedFinding_ShipsAsItIs()
    {
        var finding = Scanner(ObservationSeverity.Critical, RealFile, 2);
        var pipeline = ScanWith([finding], [finding]);
        var refuter = new ScriptedRefuter(c => [new FindingRefutation($"{RealFile}:2", true, Id: c[0].Id)]);

        var delivered = await Substantiate(pipeline, refuter);

        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.Critical,
            "a real critical must still get through loudly");
    }

    [Fact]
    public async Task Substantiation_DependencyCve_IsNotPutToTheSourceRefuter()
    {
        // Reading today's source refutes neither a vulnerable package nor a secret that
        // is already in git history — the merge has reasoned this way since p0333.
        var cve = new SkillObservation(
            Id: 0, Role: "dependency-auditor", Concern: ObservationConcern.Security,
            Description: "CVE-0000-0000 in a transitive package", Suggestion: "", Blocking: false,
            Severity: ObservationSeverity.Critical, Confidence: 90);
        var pipeline = ScanWith([cve], [cve]);
        var refuter = new ScriptedRefuter([]);

        var delivered = await Substantiate(pipeline, refuter);

        refuter.Asked.Should().BeEmpty();
        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.Critical);
    }

    [Fact]
    public async Task Substantiation_NoSandbox_LeavesEveryFindingUntouched()
    {
        var finding = Scanner(ObservationSeverity.Critical, RealFile, 2);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SkillObservations, new List<SkillObservation> { finding });
        pipeline.Set(ContextKeys.UnvouchedFindings, new List<SkillObservation> { finding });

        var delivered = await Substantiate(pipeline, new ScriptedRefuter([]));

        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.Critical,
            "a scan with nothing to read cannot substantiate, and must not go quiet instead");
    }

    private static async Task<IReadOnlyList<SkillObservation>> Substantiate(
        PipelineContext pipeline, IFindingRefuter refuter)
    {
        var source = new ScannedSourceStub(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RealFile] = $"class Config\n{{\n{SecretLine}\n}}\n",
        });
        var substantiator = new FindingSubstantiator(
            new CandidateFindingFactory(
                new SourceCitationResolver(new CitedCodeWindow()),
                new EndpointCitationResolver(),
                NullLogger<CandidateFindingFactory>.Instance),
            refuter,
            new RefutationRouter(NullLogger<RefutationRouter>.Instance),
            new RefutationVerdicts(NullLogger<RefutationVerdicts>.Instance),
            new ScanEvidenceFactory(source),
            NullLogger<FindingSubstantiator>.Instance);
        return await substantiator.SubstantiateAsync(pipeline, CancellationToken.None);
    }

    private static PipelineContext ScanWith(
        List<SkillObservation> delivered, List<SkillObservation> unvouched)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SkillObservations, delivered);
        pipeline.Set(ContextKeys.UnvouchedFindings, unvouched);
        pipeline.Set<ISandbox>(ContextKeys.Sandbox, new StubSandbox());
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("security-scan", new AgentConfig(), "skills", null));
        return pipeline;
    }

    private static SkillObservation Scanner(
        ObservationSeverity severity, string file, int line, string description = "hardcoded credential") =>
        new(Id: 0, Role: "static-pattern-scanner", Concern: ObservationConcern.Security,
            Description: description, Suggestion: "", Blocking: true, Severity: severity,
            Confidence: 80, File: file, StartLine: line,
            EvidenceMode: EvidenceMode.AnalyzedFromSource, Category: "secrets");
}
