using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0277: MergeMasterFindings routes the security-master's triaged observation array
/// into delivery (refine-with-safety-net): SkillObservations = master-curated set +
/// every uncovered High+ deterministic scanner fact; low/medium scanner noise the
/// master does not re-state is suppressed. Gated on output_schema == observation.
/// </summary>
public sealed class MergeMasterFindingsHandlerTests
{
    private const string Master = "security-master";

    [Fact]
    public async Task MergeMasterFindings_KeepsHighSeverityDeterministicRaw_EvenWhenMasterOmitsIt()
    {
        var raw = new List<SkillObservation>
        {
            Scanner("static-pattern-scanner", ObservationSeverity.High, "src/A.cs", 10),
            Scanner("static-pattern-scanner", ObservationSeverity.Low, "src/B.cs", 20),
        };
        // Master adds one analysis finding elsewhere and omits the High raw entirely.
        var answer = """
            [{"concern":"security","severity":"medium","category":"auth",
              "description":"src/C.cs:30: missing authorization check on admin action","file":"src/C.cs","start_line":30,
              "evidence_mode":"analyzed_from_source","suggestion":"Add an ownership/role check."}]
            """;
        var pipeline = PipelineWith(Master, answer, raw);

        var result = await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var delivered = Delivered(pipeline);
        delivered.Should().Contain(o => o.File == "src/A.cs" && o.Severity == ObservationSeverity.High,
            "an uncovered High deterministic fact must survive even when the master omits it");
        delivered.Should().Contain(o => o.File == "src/C.cs", "the master's analysis finding ships");
        delivered.Should().NotContain(o => o.File == "src/B.cs", "low scanner noise is suppressed");
    }

    [Fact]
    public async Task MergeMasterFindings_MasterSuppressesLowSeverityScannerNoise_NotReDelivered()
    {
        var raw = new List<SkillObservation>
        {
            Scanner("static-pattern-scanner", ObservationSeverity.Low, "src/B.cs", 20),
            Scanner("dependency-auditor", ObservationSeverity.Medium, null, 0),
        };
        var pipeline = PipelineWith(Master, "[]", raw); // master triaged to nothing

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        Delivered(pipeline).Should().BeEmpty("no High+ raw and the master kept nothing");
    }

    [Fact]
    public async Task MergeMasterFindings_SameLocationCollision_PrefersMasterObservation()
    {
        var raw = new List<SkillObservation>
        {
            Scanner("static-pattern-scanner", ObservationSeverity.High, "src/A.cs", 10, "raw scanner headline"),
        };
        // Master addresses the SAME file:line, recalibrating it down to Medium.
        var answer = """
            [{"concern":"security","severity":"medium","category":"injection",
              "description":"src/A.cs:10: parameterized after review — lower risk","file":"src/A.cs","start_line":10,
              "evidence_mode":"analyzed_from_source","suggestion":"Tracked; low priority."}]
            """;
        var pipeline = PipelineWith(Master, answer, raw);

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        var delivered = Delivered(pipeline);
        delivered.Should().ContainSingle("the raw High at the same location collapses into the master's version");
        delivered[0].Severity.Should().Be(ObservationSeverity.Medium);
        delivered[0].Description.Should().Contain("lower risk");
    }

    [Fact]
    public async Task MergeMasterFindings_EmptyButValidArray_SuppressesLowKeepsHighRaw()
    {
        var raw = new List<SkillObservation>
        {
            Scanner("git-history-scanner", ObservationSeverity.High, "src/secret.cs", 5),
            Scanner("static-pattern-scanner", ObservationSeverity.Low, "src/B.cs", 20),
        };
        var pipeline = PipelineWith(Master, "[]", raw);

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        var delivered = Delivered(pipeline);
        delivered.Should().ContainSingle();
        delivered[0].File.Should().Be("src/secret.cs");
        delivered[0].Severity.Should().Be(ObservationSeverity.High);
    }

    [Fact]
    public async Task MergeMasterFindings_UnparseableAnswer_LeavesRawObservationsUntouched()
    {
        var raw = new List<SkillObservation>
        {
            Scanner("static-pattern-scanner", ObservationSeverity.High, "src/A.cs", 10),
            Scanner("static-pattern-scanner", ObservationSeverity.Low, "src/B.cs", 20),
        };
        var pipeline = PipelineWith(Master, "I reviewed the scanners; nothing structured to report.", raw);

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        Delivered(pipeline).Should().HaveCount(2, "a non-array answer must not shrink the delivered set below today");
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.RawScannerObservations, out _)
            .Should().BeFalse("no merge ran, so no raw stash");
    }

    [Fact]
    public async Task MergeMasterFindings_CodingMasterOutputSchema_SkipsMerge()
    {
        var raw = new List<SkillObservation> { Scanner("static-pattern-scanner", ObservationSeverity.High, "src/A.cs", 10) };
        var answer = """[{"concern":"security","severity":"high","description":"should not be scraped"}]""";
        var pipeline = PipelineWith("coding-agent-master", answer, raw);

        await Build("diff").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        Delivered(pipeline).Should().HaveCount(1, "a non-observation master leaves the raw set untouched");
    }

    [Fact]
    public async Task Merge_StaticPatternHighInReadFile_SuppressedAsMasterReviewed()
    {
        var raw = new List<SkillObservation> { Scanner("static-pattern-scanner", ObservationSeverity.Critical, "src/A.cs", 19) };
        // Master read src/A.cs and triaged to nothing — an implicit rejection, not a gap.
        var pipeline = PipelineWith(Master, "[]", raw, readPaths: ["src/A.cs"]);

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        Delivered(pipeline).Should().BeEmpty(
            "a static-pattern fact in a file the master read-and-dismissed must not ship");
    }

    [Fact]
    public async Task Merge_StaticPatternHighInUnreadFile_StillPromoted()
    {
        var raw = new List<SkillObservation> { Scanner("static-pattern-scanner", ObservationSeverity.High, "src/A.cs", 10) };
        var pipeline = PipelineWith(Master, "[]", raw, readPaths: ["src/other.cs"]);

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        Delivered(pipeline).Should().ContainSingle(o => o.File == "src/A.cs",
            "a genuine coverage gap (file the master never opened) still promotes");
    }

    [Fact]
    public async Task Merge_GitHistorySecret_PromotedEvenWhenFileRead()
    {
        var raw = new List<SkillObservation> { Scanner("git-history-scanner", ObservationSeverity.Critical, "src/secret.cs", 5) };
        // Reading the current file does not refute a historical leak.
        var pipeline = PipelineWith(Master, "[]", raw, readPaths: ["src/secret.cs"]);

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        Delivered(pipeline).Should().ContainSingle(o => o.File == "src/secret.cs",
            "a git-history secret is never suppressed by the read-set");
    }

    [Fact]
    public async Task Merge_DependencyCve_PromotedEvenWhenFileRead()
    {
        var raw = new List<SkillObservation> { Scanner("dependency-auditor", ObservationSeverity.High, "src/App.csproj", 1) };
        // A vulnerable package is not refuted by reading the project file.
        var pipeline = PipelineWith(Master, "[]", raw, readPaths: ["src/App.csproj"]);

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        Delivered(pipeline).Should().ContainSingle(o => o.File == "src/App.csproj",
            "a dependency CVE is never suppressed by the read-set");
    }

    [Fact]
    public async Task Merge_EmptyReadSet_PromotesAllUncoveredHighPlus_RegressionGuard()
    {
        var raw = new List<SkillObservation> { Scanner("static-pattern-scanner", ObservationSeverity.High, "src/A.cs", 10) };
        // No read-set = no evidence the master looked -> p0277 safety net stays intact.
        var pipeline = PipelineWith(Master, "[]", raw);

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        Delivered(pipeline).Should().ContainSingle(o => o.File == "src/A.cs",
            "with no read-set, uncovered High+ still promotes as it does today");
    }

    [Fact]
    public async Task Merge_ReadPathWorkdirPrefixMismatch_NormalizedMatchStillSuppresses()
    {
        var raw = new List<SkillObservation> { Scanner("static-pattern-scanner", ObservationSeverity.Critical, "src/A.cs", 19) };
        // Read-set carries the sandbox workdir/context prefix; scanner File does not.
        var pipeline = PipelineWith(Master, "[]", raw, readPaths: ["default/src/A.cs"]);

        await Build("observation").ExecuteAsync(
            new MergeMasterFindingsContext(pipeline), CancellationToken.None);

        Delivered(pipeline).Should().BeEmpty(
            "normalized suffix match absorbs the 'default/' prefix so the fact is still suppressed");
    }

    private static SkillObservation Scanner(
        string role, ObservationSeverity severity, string? file, int line, string description = "scanner finding") =>
        new(Id: 0, Role: role, Concern: ObservationConcern.Security, Description: description,
            Suggestion: "", Blocking: false, Severity: severity, Confidence: 80,
            File: file, StartLine: line,
            EvidenceMode: EvidenceMode.AnalyzedFromSource, Category: "security");

    private static PipelineContext PipelineWith(
        string masterSkill, string answer, List<SkillObservation> raw, List<string>? readPaths = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.MasterSkillName, masterSkill);
        pipeline.Set(ContextKeys.MasterAnswer, answer);
        pipeline.Set(ContextKeys.SkillObservations, raw);
        if (readPaths is not null)
            pipeline.Set(ContextKeys.MasterReadPaths, readPaths);
        return pipeline;
    }

    private static List<SkillObservation> Delivered(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var obs) && obs is not null
            ? obs
            : [];

    private static MergeMasterFindingsHandler Build(string? resolvedSchema) =>
        new(new StubSchemaResolver(resolvedSchema),
            TolerantJsonParserFactory.CreateObservation(),
            TolerantJsonParserFactory.CreateTolerant(),
            NullLogger<MergeMasterFindingsHandler>.Instance);

    private sealed class StubSchemaResolver(string? schema) : IMasterOutputSchemaResolver
    {
        public string? Resolve(string masterSkillName) => schema;
    }
}
