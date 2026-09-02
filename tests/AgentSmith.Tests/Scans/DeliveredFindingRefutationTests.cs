using System.Text;
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
/// 2026-09-01-85b2: the refutation step used to be offered ONLY the findings a master's
/// silence promoted. On a repo scan the master curates everything, so the step was handed
/// nothing and said so in five of six observed runs — a verification step that by
/// construction never saw what the run delivered.
/// <para>
/// Widening the selection without changing the fates would have shipped a DELETION: an
/// unresolvable citation removes a finding from delivery, and a master finding cites a file
/// with a line, which is exactly what makes the source resolver claim it. These tests pin
/// the asymmetry — every delivered finding is CHECKED, and only a finding nobody authored
/// can be DELETED for a citation the reader could not resolve.
/// </para>
/// </summary>
public sealed class DeliveredFindingRefutationTests
{
    private const string SecretLine = "    var apiKey = \"AKIA0000EXAMPLE\";";
    private const string RealFile = "src/Config.cs";
    private const string SecondRepoFile = "worker/src/Queue.cs";
    private const string QueueLine = "    var connection = \"Endpoint=sb://example\";";

    [Fact]
    public async Task Refuter_IsAskedAboutAMastersOwnFinding()
    {
        var authored = Master(ObservationSeverity.Critical, RealFile, 2);
        var refuter = new ScriptedRefuter([]);

        var delivered = await Substantiate(Scan([authored]), refuter);

        refuter.Asked.Should().ContainSingle(
            "a master that curates everything used to leave the refuter nothing to check")
            .Which.Observation.Should().Be(authored);
        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.Critical);
    }

    /// <summary>
    /// The one thing this phase must not do. The source resolver claims essentially every
    /// AnalyzedFromSource finding with a file and a line, so the moment master findings
    /// enter the checked set, every one whose path the reader cannot open would be dropped
    /// as an invention — the opposite of the phase's purpose.
    /// </summary>
    [Fact]
    public async Task Refuter_MasterFindingWhoseFileCannotBeRead_IsStillDelivered()
    {
        var authored = Master(ObservationSeverity.Critical, "src/Unreadable.cs", 12);

        var delivered = await Substantiate(Scan([authored]), new ScriptedRefuter([]));

        delivered.Should().ContainSingle("a reader that could not read is not a master that "
            + "invented a file").Which.Should().Be(authored);
    }

    /// <summary>The unauthored fate is unchanged: nobody read it, so an invented path still costs it.</summary>
    [Fact]
    public async Task Refuter_PromotedFindingWhoseFileCannotBeRead_IsStillDropped()
    {
        var promoted = Master(ObservationSeverity.Critical, "src/Imagined.cs", 4) with
        {
            Role = "static-pattern-scanner",
        };
        var pipeline = Scan([promoted]);
        pipeline.Set(ContextKeys.UnvouchedFindings, new List<SkillObservation> { promoted });

        var delivered = await Substantiate(pipeline, new ScriptedRefuter([]));

        delivered.Should().BeEmpty("a finding the master never addressed has no author");
    }

    [Fact]
    public async Task Refuter_MasterFindingInASecondRepository_IsResolved()
    {
        var finding = Master(ObservationSeverity.High, SecondRepoFile, 2);
        var refuter = new ScriptedRefuter([]);

        var delivered = await Substantiate(TwoRepoScan([finding]), refuter, TwoRepoReaders());

        refuter.Asked.Should().ContainSingle(
            "the master addresses every repository in the run, so the evidence must be "
            + "looked for in every sandbox")
            .Which.Evidence.Should().Contain(QueueLine);
        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.High);
    }

    [Fact]
    public async Task Refuter_IsNotAskedAboutALineTheFileDoesNotHave()
    {
        var finding = Master(ObservationSeverity.Critical, RealFile, 400);
        var refuter = new ScriptedRefuter([]);

        var delivered = await Substantiate(Scan([finding]), refuter);

        refuter.Asked.Should().BeEmpty("a window around a line the file does not have is a "
            + "sentence saying so, and a refuter can satisfy the quote check by quoting it");
        delivered.Should().ContainSingle().Which.Severity.Should().Be(ObservationSeverity.Critical);
    }

    [Fact]
    public async Task Refuter_TwoFindingsAtOneLocation_AreAnsweredSeparately()
    {
        var authorization = Master(ObservationSeverity.Critical, RealFile, 2, "the endpoint has no authorization check");
        var injection = Master(ObservationSeverity.Critical, RealFile, 2, "the value is concatenated into a query");
        var refuter = new ScriptedRefuter(c =>
            [new FindingRefutation(c[1].Location, false, SecretLine.Trim(), "a sample value", c[1].Id)]);

        var delivered = await Substantiate(Scan([authorization, injection]), refuter);

        delivered.Should().HaveCount(2);
        delivered.Single(o => o.Description == authorization.Description).Severity
            .Should().Be(ObservationSeverity.Critical, "nobody refuted this one");
        delivered.Single(o => o.Description == injection.Description).Severity
            .Should().Be(ObservationSeverity.Medium);
    }

    /// <summary>
    /// One call carries every candidate, so what breaks at thirty findings is the ANSWER
    /// running out of room. A strict deserialize threw, the reader returned null and the
    /// whole step no-opped: it failed open, which is safe, and silent, which is not.
    /// </summary>
    [Fact]
    public async Task Refuter_ManyFindings_AnswerIsReadEvenWhenTruncated()
    {
        var findings = Enumerable.Range(1, 30)
            .Select(i => Master(ObservationSeverity.Critical, RealFile, 2, $"claim number {i}"))
            .ToList();
        var reader = new FindingRefutationReader(
            TolerantJsonParserFactory.CreateTolerant(),
            NullLogger<FindingRefutationReader>.Instance);
        var refuter = new ScriptedRefuter(c => reader.Read(TruncatedAnswer(c)));

        var delivered = await Substantiate(Scan(findings), refuter);

        delivered.Should().HaveCount(30, "a truncated answer never deletes anything");
        delivered.Count(o => o.Severity == ObservationSeverity.Medium)
            .Should().Be(29, "every COMPLETE verdict in the cut-off array is a real verdict");
        delivered.Count(o => o.Severity == ObservationSeverity.Critical)
            .Should().Be(1, "the row the answer never finished writing is not a verdict");
    }

    [Fact]
    public async Task Refuter_Silence_LeavesEveryFindingStanding()
    {
        var readable = Master(ObservationSeverity.Critical, RealFile, 2, "a readable citation");
        var unreadable = Master(ObservationSeverity.Critical, "src/Unreadable.cs", 3, "an unreadable citation");
        var beyond = Master(ObservationSeverity.High, RealFile, 900, "a line past the end");

        var delivered = await Substantiate(
            Scan([readable, unreadable, beyond]), ScriptedRefuter.Unreachable());

        delivered.Should().HaveCount(3);
        delivered.Should().OnlyContain(o => o.ReviewStatus != RefutedFinding.ReviewStatus,
            "a refuter that could not be asked, or answered unreadably, decides nothing");
        delivered.Should().Contain(unreadable);
    }

    /// <summary>
    /// SubstantiateFindings is in the pr-review preset too, and a pr-review master writes
    /// every finding itself — the widened selection has its largest effect there.
    /// </summary>
    [Fact]
    public async Task Refuter_PrReviewFindings_AreCheckedAndNeverDeleted()
    {
        PipelinePresets.PrReview.Should().Contain(CommandNames.SubstantiateFindings);
        var readable = Master(ObservationSeverity.High, RealFile, 2, "the handler swallows the exception")
            with { Role = "pr-review-master" };
        var unreadable = Master(ObservationSeverity.High, "src/Renamed.cs", 8, "the branch is unreachable")
            with { Role = "pr-review-master" };
        var pipeline = Scan([readable, unreadable], "pr-review");
        var refuter = new ScriptedRefuter([]);

        var delivered = await Substantiate(pipeline, refuter);

        refuter.Asked.Should().ContainSingle().Which.Observation.Should().Be(readable);
        delivered.Should().HaveCount(2, "a review finding is never deleted by this step");
    }

    /// <summary>Every verdict but the last, then a cut in the middle of the last row.</summary>
    private static string TruncatedAnswer(IReadOnlyList<CandidateFinding> candidates)
    {
        var rows = new StringBuilder("[\n");
        foreach (var candidate in candidates)
            rows.Append("  {\"id\": \"").Append(candidate.Id)
                .Append("\", \"location\": \"").Append(candidate.Location)
                .Append("\", \"substantiated\": false, \"quote\": \"").Append(SecretLine.Trim().Replace("\"", "\\\""))
                .Append("\", \"why\": \"a sample value\"},\n");
        var text = rows.ToString();
        return text[..text.LastIndexOf("\"why\"", StringComparison.Ordinal)];
    }

    private static async Task<IReadOnlyList<SkillObservation>> Substantiate(
        PipelineContext pipeline, IFindingRefuter refuter, ISandboxFileReaderFactory? readers = null)
    {
        var substantiator = new FindingSubstantiator(
            new CandidateFindingFactory(
                new SourceCitationResolver(new CitedCodeWindow()),
                new EndpointCitationResolver(),
                NullLogger<CandidateFindingFactory>.Instance),
            refuter,
            new RefutationRouter(NullLogger<RefutationRouter>.Instance),
            new RefutationVerdicts(NullLogger<RefutationVerdicts>.Instance),
            new ScanEvidenceFactory(readers ?? DefaultRepo()),
            NullLogger<FindingSubstantiator>.Instance);
        return await substantiator.SubstantiateAsync(pipeline, CancellationToken.None);
    }

    private static ScannedSourceStub DefaultRepo() =>
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RealFile] = $"class Config\n{{\n{SecretLine}\n}}\n",
        });

    private static PipelineContext Scan(
        List<SkillObservation> delivered, string pipelineName = "security-scan")
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SkillObservations, delivered);
        pipeline.Set<ISandbox>(ContextKeys.Sandbox, new StubSandbox());
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig(pipelineName, new AgentConfig(), "skills", null));
        return pipeline;
    }

    private static readonly ISandbox ApiRepo = new StubSandbox();
    private static readonly ISandbox WorkerRepo = new StubSandbox();

    private static PipelineContext TwoRepoScan(List<SkillObservation> delivered)
    {
        var pipeline = Scan(delivered);
        pipeline.Set<ISandbox>(ContextKeys.Sandbox, ApiRepo);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            {
                ["api"] = ApiRepo,
                ["worker"] = WorkerRepo,
            });
        return pipeline;
    }

    private static ScannedReposStub TwoRepoReaders() =>
        new(new Dictionary<ISandbox, ISandboxFileReader>
        {
            [ApiRepo] = DefaultRepo(),
            [WorkerRepo] = new ScannedSourceStub(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // The master cites what its tools showed it, prefix included; the checkout
                // has the path without it.
                ["src/Queue.cs"] = $"class Queue\n{{\n{QueueLine}\n}}\n",
            }),
        });

    private static SkillObservation Master(
        ObservationSeverity severity, string file, int line,
        string description = "hardcoded credential") =>
        new(Id: 0, Role: "security-master", Concern: ObservationConcern.Security,
            Description: description, Suggestion: "", Blocking: true, Severity: severity,
            Confidence: 80, File: file, StartLine: line,
            EvidenceMode: EvidenceMode.AnalyzedFromSource, Category: "secrets");
}
