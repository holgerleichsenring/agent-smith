using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0169j-b1 failure-reason completeness gate — mirrors the success-path
/// Theory in <see cref="EventSequenceCompletenessTests"/>. Every failing
/// producer must surface its reason as a typed event field, not as a
/// stderr-only log. A regression where a producer goes silent on failure
/// fails its row.
/// </summary>
public sealed class EventFailureReasonCompletenessTests : IDisposable
{
    private const string RunId = "2026-05-27T11-00-00-bbbb";

    private readonly string _tempDir;

    public EventFailureReasonCompletenessTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-failure-reason-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task StepFinished_StatusFailed_ReasonPopulated()
    {
        var recorder = new RecordingEventPublisher();
        await recorder.PublishAsync(new StepFinishedEvent(
            RunId, 1, "failed", 250L, DateTimeOffset.UtcNow, "command exec returned exit 1"));

        var stepFinished = recorder.Events.OfType<StepFinishedEvent>().Single();
        stepFinished.Status.Should().Be("failed");
        stepFinished.Reason.Should().Be("command exec returned exit 1");
    }

    [Fact]
    public async Task ToolResult_OkFalse_ErrorMessagePopulated()
    {
        var recorder = new RecordingEventPublisher();
        var throwing = AIFunctionFactory.Create(
            (string path) =>
            {
                throw new InvalidOperationException("boom: file not found");
#pragma warning disable CS0162
                return "unreachable";
#pragma warning restore CS0162
            },
            name: "read_file");
        var wrapped = new EventPublishingAIFunction(
            throwing, recorder, new ScopedRunContext(RunId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            wrapped.InvokeAsync(new AIFunctionArguments { ["path"] = "/work/foo" }, CancellationToken.None).AsTask());

        var toolResult = recorder.Events.OfType<ToolResultEvent>().Single();
        toolResult.Ok.Should().BeFalse();
        toolResult.ErrorMessage.Should().Be("boom: file not found");
    }

    [Fact]
    public void CatalogIssue_SkillValidationFailure_EmitsWarningSeverity()
    {
        var recorder = new RecordingEventPublisher();
        var runContext = new ScopedRunContext(RunId);
        var loader = BuildSkillLoader(recorder, runContext);

        var skillsDir = Path.Combine(_tempDir, "skills");
        Directory.CreateDirectory(skillsDir);
        WriteSkillWithOversizedDescription(skillsDir);

        loader.LoadRoleDefinitions(skillsDir);

        var issue = recorder.Events.OfType<CatalogIssueEvent>().SingleOrDefault();
        issue.Should().NotBeNull("a skill-validation failure must surface as a CatalogIssueEvent");
        issue!.Severity.Should().Be("warning");
        issue.Category.Should().Be("skill-validation");
        issue.Message.Should().Contain("description must be at most");
    }

    [Fact]
    public void CatalogIssue_VocabularyParseFailure_EmitsErrorSeverity()
    {
        var recorder = new RecordingEventPublisher();
        var runContext = new ScopedRunContext(RunId);
        var loader = new ConceptVocabularyLoader(
            recorder, runContext, new NoOpSystemEventPublisher(), NullLogger<ConceptVocabularyLoader>.Instance);

        WriteLegacyVocabulary(_tempDir);

        Assert.Throws<InvalidOperationException>(() => loader.Load(_tempDir));

        var issue = recorder.Events.OfType<CatalogIssueEvent>().SingleOrDefault();
        issue.Should().NotBeNull("a vocabulary parse failure must surface as a CatalogIssueEvent");
        issue!.Severity.Should().Be("error");
        issue.Category.Should().Be("vocabulary-parse");
    }

    private YamlSkillLoader BuildSkillLoader(IEventPublisher publisher, IRunContextAccessor runContext) =>
        new(
            new StubSkillsCatalogPath(),
            new ConceptVocabularyLoader(publisher, runContext, new NoOpSystemEventPublisher(), NullLogger<ConceptVocabularyLoader>.Instance),
            new ConceptVocabularyValidator(NullLogger<ConceptVocabularyValidator>.Instance),
            new SkillIndexBuilder(NullLogger<SkillIndexBuilder>.Instance),
            new ProviderOverrideResolver(new ActiveProviderResolver(new AgentSmithConfig())),
            publisher,
            runContext,
            new NoOpSystemEventPublisher(),
            NullLogger<YamlSkillLoader>.Instance);

    private static void WriteSkillWithOversizedDescription(string skillsDir)
    {
        var skillDir = Path.Combine(skillsDir, "oversized-description");
        Directory.CreateDirectory(skillDir);
        var oversized = new string('x', 220);
        var content = $$"""
            ---
            name: "oversized-description"
            version: "1.0.0"
            description: "{{oversized}}"
            role: "producer"
            output_schema: "observation"
            activates_when: 'pipeline_name = "test"'
            ---

            Body.
            """;
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), content);
    }

    private static void WriteLegacyVocabulary(string dir)
    {
        var content = """
            vocabulary:
              project_concepts:
                - name: foo
                  type: bool
              change_signals: []
              run_context: []
            """;
        File.WriteAllText(Path.Combine(dir, "concept-vocabulary.yaml"), content);
    }

    private sealed class ScopedRunContext(string runId) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public IDisposable BeginScope(string id) => new NoOpScope();
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }
}
