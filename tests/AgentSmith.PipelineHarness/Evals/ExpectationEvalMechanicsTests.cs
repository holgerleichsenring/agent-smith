using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.Expectations;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: the LLM-free tier of the replay-goldens mechanics — fixture load
/// (shape + anonymization enforcement on BOTH the load and ingestion paths)
/// and the eval loop end-to-end with scripted draft + judge calls, down to
/// the persisted report artifact.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class ExpectationEvalMechanicsTests
{
    private static string GoldensDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ExpectationGoldens");

    [Fact]
    public void ExpectationFixture_Load_ValidatesShapeAndAnonymization()
    {
        var fixtures = ExpectationFixtureLoader.LoadAll(GoldensDirectory);
        fixtures.Should().ContainSingle("the repo ships one clearly-synthetic example fixture")
            .Which.Gold!.Expected.Should().NotBeEmpty();

        var dir = CreateTempDirectory();
        try
        {
            var path = Path.Combine(dir, "leaky.json");
            File.WriteAllText(path, LeakyFixtureJson());
            var load = () => ExpectationFixtureLoader.Load(path);
            load.Should().Throw<InvalidDataException>()
                .Which.Message.Should()
                .Contain("attestation", "an unattested fixture is rejected")
                .And.Contain("email address", "generic fingerprint patterns are enforced")
                .And.Contain("URL", "non-placeholder URLs are fingerprints");

            File.WriteAllText(Path.Combine(dir, ExpectationFixtureAnonymizationCheck.DenyListFileName),
                @"\bproject-nimbus\b");
            var denied = Fixture() with
            {
                Ticket = new ExpectationFixture.TicketMaterial(
                    "Project-Nimbus export hangs", "The export never finishes.", null),
            };
            var ingest = () => ExpectationFixtureIngestion.Ingest(denied, dir);
            ingest.Should().Throw<InvalidDataException>("the deny-list file extends the checks")
                .Which.Message.Should().Contain("project-nimbus");
            Directory.GetFiles(dir, "*.json").Should().HaveCount(1,
                "a refused ingestion writes NOTHING");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task EvalHarness_ScriptedDrafts_ProducesJudgeReport()
    {
        var scripted = new ScriptedChatClient()
            .EnqueueText(DraftJson)
            .EnqueueText("""{"matches":[{"gold":1,"draft":1},{"gold":2,"draft":2},{"gold":3,"draft":3}]}""");
        var harness = new ExpectationEvalHarness(Drafter(scripted), new ExpectationEvalJudge(scripted));

        var report = await harness.RunAsync(
            ExpectationFixtureLoader.LoadAll(GoldensDirectory), new AgentConfig(),
            "scripted-fixture-model", "v0.0.0-scripted", CancellationToken.None);

        var entry = report.Entries.Should().ContainSingle().Subject;
        entry.Verdict!.MatchedCount.Should().Be(3);
        entry.Verdict.MissedCount.Should().Be(1, "gold assertion 4 has no draft counterpart");
        entry.Verdict.Hallucinated.Should().ContainSingle("draft assertion 4 covers no gold")
            .Which.Should().Contain("telemetry");
        report.MatchedRate.Should().BeApproximately(0.75, 0.001);

        var dir = CreateTempDirectory();
        try
        {
            var mdPath = ExpectationEvalReportWriter.Write(report, dir);
            File.Exists(Path.ChangeExtension(mdPath, ".json")).Should().BeTrue();
            var markdown = File.ReadAllText(mdPath);
            markdown.Should().Contain("scripted-fixture-model", "the header pins the model id")
                .And.Contain("v0.0.0-scripted", "the header pins the skills version")
                .And.Contain("missed:").And.Contain("hallucinated:").And.Contain("matched:");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // The scripted draft: three assertions matching gold 1-3, plus a fourth
    // (telemetry) no gold assertion asked for — the hallucination case.
    private const string DraftJson = """
        {"observed": "Duplicate widget names vanish silently during CSV import.",
         "expected": [
           "A duplicate-name row is reported as rejected in the import summary.",
           "The imported count in the summary equals the persisted row count.",
           "Unique-name CSV imports behave exactly as before.",
           "The importer emits a telemetry counter for rejected rows."],
         "constraints": ["No schema change to the widgets table."],
         "open_question": null}
        """;

    private static ExpectationDrafter Drafter(ScriptedChatClient scripted) => new(
        new ScriptedChatClientFactoryAdapter(scripted),
        new EmbeddedPromptCatalog(
            new EnvDirectoryPromptOverrideSource(NullLogger<EnvDirectoryPromptOverrideSource>.Instance),
            NullLogger<EmbeddedPromptCatalog>.Instance),
        new ExpectationDraftValidator(),
        new EvalRunContext(),
        NullLogger<ExpectationDrafter>.Instance);

    private static ExpectationFixture Fixture() => new(
        1, "temp-fixture", Synthetic: true,
        new ExpectationFixture.Attestation(true, "tester", null),
        new ExpectationFixture.TicketMaterial("A title", "A description.", null),
        null,
        new ExpectationDraft("Observed.", ["The fix is verifiable."], [], null));

    private static string LeakyFixtureJson() => """
        {
          "version": 1,
          "id": "leaky",
          "synthetic": false,
          "anonymization": { "attested": false },
          "ticket": {
            "title": "Login broken",
            "description": "Reported by jane.doe@internal-corp.io via https://tickets.internal-corp.io/browse/OPS-1."
          },
          "gold": { "observed": "Login fails.", "expected": ["Login succeeds."], "constraints": [] }
        }
        """;

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"agentsmith-goldens-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
