using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class SkillManagerTests : IDisposable
{
    private readonly string _tempDir;

    public SkillManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-skill-mgr-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Pipeline Preset

    [Fact]
    public void SkillManager_Preset_Resolves()
    {
        var result = PipelinePresets.TryResolve("skill-manager");

        result.Should().NotBeNull();
        result!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SkillManager_Preset_ContainsExpectedCommands()
    {
        PipelinePresets.SkillManager.Should().Contain(CommandNames.DiscoverSkills);
        PipelinePresets.SkillManager.Should().Contain(CommandNames.EvaluateSkills);
        PipelinePresets.SkillManager.Should().Contain(CommandNames.DraftSkillFiles);
        PipelinePresets.SkillManager.Should().Contain(CommandNames.ApproveSkills);
        PipelinePresets.SkillManager.Should().Contain(CommandNames.InstallSkills);
        PipelinePresets.SkillManager.Should().Contain(CommandNames.WriteRunResult);
    }

    [Fact]
    public void SkillManager_Preset_CaseInsensitive()
    {
        PipelinePresets.TryResolve("Skill-Manager").Should().NotBeNull();
        PipelinePresets.TryResolve("SKILL-MANAGER").Should().NotBeNull();
    }

    [Fact]
    public void SkillManager_DefaultSkillsPath_IsCoding()
    {
        PipelinePresets.GetDefaultSkillsPath("skill-manager").Should().Be("skills/coding");
    }

    #endregion

    #region Model Construction

    [Fact]
    public void SkillCandidate_CanBeConstructed()
    {
        var candidate = new SkillCandidate(
            "test-skill", "A test skill", "https://example.com", "# Skill\nContent", "1.0", "abc123");

        candidate.Name.Should().Be("test-skill");
        candidate.Description.Should().Be("A test skill");
        candidate.SourceUrl.Should().Be("https://example.com");
        candidate.Content.Should().Contain("# Skill");
        candidate.Version.Should().Be("1.0");
        candidate.Commit.Should().Be("abc123");
    }

    [Fact]
    public void SkillCandidate_NullableFields_CanBeNull()
    {
        var candidate = new SkillCandidate("test", "desc", "url", "content", null, null);

        candidate.Version.Should().BeNull();
        candidate.Commit.Should().BeNull();
    }

    [Fact]
    public void SkillEvaluation_CanBeConstructed()
    {
        var candidate = new SkillCandidate("test", "desc", "url", "content", null, null);
        var evaluation = new SkillEvaluation(
            candidate, 8, 9, "Good fit", "Safe", "install", false, null);

        evaluation.Candidate.Should().Be(candidate);
        evaluation.FitScore.Should().Be(8);
        evaluation.SafetyScore.Should().Be(9);
        evaluation.FitReasoning.Should().Be("Good fit");
        evaluation.SafetyReasoning.Should().Be("Safe");
        evaluation.Recommendation.Should().Be("install");
        evaluation.HasOverlap.Should().BeFalse();
        evaluation.OverlapWith.Should().BeNull();
    }

    [Fact]
    public void SkillEvaluation_WithOverlap()
    {
        var candidate = new SkillCandidate("test", "desc", "url", "content", null, null);
        var evaluation = new SkillEvaluation(
            candidate, 6, 8, "Partial fit", "Safe", "review", true, "existing-skill");

        evaluation.HasOverlap.Should().BeTrue();
        evaluation.OverlapWith.Should().Be("existing-skill");
    }

    #endregion

    #region DiscoverSkillsHandler

    [Fact]
    public async Task DiscoverSkills_NoSourcesDir_ReturnsEmpty()
    {
        var handler = new DiscoverSkillsHandler(NullLogger<DiscoverSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new DiscoverSkillsContext(
            Path.Combine(_tempDir, "nonexistent"), [], pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<IReadOnlyList<SkillCandidate>>(ContextKeys.SkillCandidates, out var candidates)
            .Should().BeTrue();
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverSkills_FindsCandidates()
    {
        // Create source directory with skill candidates
        var sourcesDir = Path.Combine(_tempDir, "sources");
        var skillDir = Path.Combine(sourcesDir, "new-skill");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# New Skill\nA great new skill");

        var handler = new DiscoverSkillsHandler(NullLogger<DiscoverSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new DiscoverSkillsContext(sourcesDir, [], pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<IReadOnlyList<SkillCandidate>>(ContextKeys.SkillCandidates, out var candidates)
            .Should().BeTrue();
        candidates.Should().HaveCount(1);
        candidates![0].Name.Should().Be("new-skill");
        candidates[0].Description.Should().Be("A great new skill");
    }

    [Fact]
    public async Task DiscoverSkills_ExcludesInstalled()
    {
        var sourcesDir = Path.Combine(_tempDir, "sources");
        var skillDir = Path.Combine(sourcesDir, "already-installed");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# Old Skill\nAlready there");

        var handler = new DiscoverSkillsHandler(NullLogger<DiscoverSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new DiscoverSkillsContext(sourcesDir, ["already-installed"], pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<IReadOnlyList<SkillCandidate>>(ContextKeys.SkillCandidates, out var candidates)
            .Should().BeTrue();
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverSkills_SkipsDirsWithoutSkillMd()
    {
        var sourcesDir = Path.Combine(_tempDir, "sources");
        Directory.CreateDirectory(Path.Combine(sourcesDir, "no-skill-file"));
        var validDir = Path.Combine(sourcesDir, "valid-skill");
        Directory.CreateDirectory(validDir);
        File.WriteAllText(Path.Combine(validDir, "SKILL.md"), "# Valid\nContent here");

        var handler = new DiscoverSkillsHandler(NullLogger<DiscoverSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new DiscoverSkillsContext(sourcesDir, [], pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<IReadOnlyList<SkillCandidate>>(ContextKeys.SkillCandidates, out var candidates)
            .Should().BeTrue();
        candidates.Should().HaveCount(1);
        candidates![0].Name.Should().Be("valid-skill");
    }

    [Theory]
    [InlineData("# Heading\nActual description", "Actual description")]
    [InlineData("Just a plain line", "Just a plain line")]
    [InlineData("# Only Heading\n\n", "No description available")]
    [InlineData("", "No description available")]
    public void ExtractDescription_ReturnsCorrectValue(string content, string expected)
    {
        DiscoverSkillsHandler.ExtractDescription(content).Should().Be(expected);
    }

    #endregion

    #region EvaluateSkillsHandler

    [Fact]
    public void ParseEvaluation_ValidResponse_ReturnsEvaluation()
    {
        var candidate = new SkillCandidate("test", "desc", "url", "content", null, null);
        var response = """
            FIT_SCORE: 8
            FIT_REASONING: Excellent match for coding pipeline
            SAFETY_SCORE: 9
            SAFETY_REASONING: No injection or exfiltration risks
            RECOMMENDATION: install
            HAS_OVERLAP: false
            OVERLAP_WITH:
            """;

        var evaluation = EvaluateSkillsHandler.ParseEvaluation(candidate, response);

        evaluation.Should().NotBeNull();
        evaluation!.FitScore.Should().Be(8);
        evaluation.SafetyScore.Should().Be(9);
        evaluation.FitReasoning.Should().Contain("Excellent match");
        evaluation.SafetyReasoning.Should().Contain("No injection");
        evaluation.Recommendation.Should().Be("install");
        evaluation.HasOverlap.Should().BeFalse();
        evaluation.OverlapWith.Should().BeNull();
    }

    [Fact]
    public void ParseEvaluation_WithOverlap_ReturnsOverlap()
    {
        var candidate = new SkillCandidate("test", "desc", "url", "content", null, null);
        var response = """
            FIT_SCORE: 6
            FIT_REASONING: Partial fit
            SAFETY_SCORE: 8
            SAFETY_REASONING: Safe
            RECOMMENDATION: review
            HAS_OVERLAP: true
            OVERLAP_WITH: architect
            """;

        var evaluation = EvaluateSkillsHandler.ParseEvaluation(candidate, response);

        evaluation.Should().NotBeNull();
        evaluation!.HasOverlap.Should().BeTrue();
        evaluation.OverlapWith.Should().Be("architect");
    }

    [Fact]
    public void ParseEvaluation_InvalidResponse_ReturnsNull()
    {
        var candidate = new SkillCandidate("test", "desc", "url", "content", null, null);
        var evaluation = EvaluateSkillsHandler.ParseEvaluation(candidate, "garbage response");

        evaluation.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateSkills_NoCandidates_ReturnsEmpty()
    {
        var llmClient = new Mock<ILlmClient>();
        var handler = new EvaluateSkillsHandler(llmClient.Object, new FakePromptCatalog(), NullLogger<EvaluateSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new EvaluateSkillsContext([], [], pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<IReadOnlyList<SkillEvaluation>>(ContextKeys.SkillEvaluations, out var evals)
            .Should().BeTrue();
        evals.Should().BeEmpty();
    }

    #endregion

    #region DraftSkillFilesHandler

    [Fact]
    public async Task DraftSkillFiles_CreatesFiles()
    {
        var handler = new DraftSkillFilesHandler(NullLogger<DraftSkillFilesHandler>.Instance);
        var candidate = new SkillCandidate("my-skill", "desc", "https://source.com", "# Skill Content", "1.0", "abc");
        var evaluation = new SkillEvaluation(candidate, 8, 9, "Good fit", "Safe", "install", false, null);
        var pipeline = new PipelineContext();
        var context = new DraftSkillFilesContext([evaluation], pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.SkillInstallPath, out var draftDir).Should().BeTrue();
        draftDir.Should().NotBeNullOrEmpty();

        var skillDir = Path.Combine(draftDir!, "my-skill");
        Directory.Exists(skillDir).Should().BeTrue();
        File.Exists(Path.Combine(skillDir, "SKILL.md")).Should().BeTrue();
        File.Exists(Path.Combine(skillDir, "agentsmith.md")).Should().BeTrue();
        File.Exists(Path.Combine(skillDir, "source.md")).Should().BeTrue();

        File.ReadAllText(Path.Combine(skillDir, "SKILL.md")).Should().Contain("# Skill Content");

        // Cleanup
        if (Directory.Exists(draftDir!))
            Directory.Delete(draftDir!, recursive: true);
    }

    [Fact]
    public async Task DraftSkillFiles_NoEvaluations_Skips()
    {
        var handler = new DraftSkillFilesHandler(NullLogger<DraftSkillFilesHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new DraftSkillFilesContext([], pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No evaluations");
    }

    [Fact]
    public void GenerateAgentsmithMd_ContainsMetadata()
    {
        var candidate = new SkillCandidate("test", "A test skill", "url", "content", null, null);
        var evaluation = new SkillEvaluation(candidate, 8, 9, "Good fit", "Safe", "install", false, null);

        var md = DraftSkillFilesHandler.GenerateAgentsmithMd(evaluation);

        md.Should().Contain("name: test");
        md.Should().Contain("fit_score: 8");
        md.Should().Contain("safety_score: 9");
        md.Should().Contain("recommendation: install");
        md.Should().Contain("Good fit");
        md.Should().Contain("Safe");
    }

    [Fact]
    public void GenerateSourceMd_ContainsProvenance()
    {
        var candidate = new SkillCandidate("test", "desc", "https://source.com", "content", "2.0", "def456");
        var evaluation = new SkillEvaluation(candidate, 8, 9, "fit", "safe", "install", false, null);

        var md = DraftSkillFilesHandler.GenerateSourceMd(evaluation);

        md.Should().Contain("origin: https://source.com");
        md.Should().Contain("version: 2.0");
        md.Should().Contain("commit: def456");
        md.Should().Contain("reviewed_by: skill-manager");
    }

    #endregion

    #region ApproveSkillsHandler

    [Fact]
    public async Task ApproveSkills_NoSkills_ReturnsEmpty()
    {
        var handler = CreateApproveHandler(approves: true);
        var pipeline = new PipelineContext();
        var context = new ApproveSkillsContext([], null, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<IReadOnlyList<SkillEvaluation>>(ContextKeys.ApprovedSkills, out var approved)
            .Should().BeTrue();
        approved.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveSkills_RejectedByDefault_ReturnsEmpty()
    {
        var handler = CreateApproveHandler(approves: false);
        var candidate = new SkillCandidate("test", "desc", "url", "content", null, null);
        var evaluation = new SkillEvaluation(candidate, 8, 9, "fit", "safe", "install", false, null);
        var pipeline = new PipelineContext();
        var context = new ApproveSkillsContext([evaluation], null, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<IReadOnlyList<SkillEvaluation>>(ContextKeys.ApprovedSkills, out var approved)
            .Should().BeTrue();
        approved.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveSkills_Approved_ReturnsSkill()
    {
        var handler = CreateApproveHandler(approves: true);
        var candidate = new SkillCandidate("test", "desc", "url", "content", null, null);
        var evaluation = new SkillEvaluation(candidate, 8, 9, "fit", "safe", "install", false, null);
        var pipeline = new PipelineContext();
        var context = new ApproveSkillsContext([evaluation], null, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<IReadOnlyList<SkillEvaluation>>(ContextKeys.ApprovedSkills, out var approved)
            .Should().BeTrue();
        approved.Should().HaveCount(1);
        approved![0].Candidate.Name.Should().Be("test");
    }

    [Fact]
    public void FormatApprovalQuestion_ContainsScores()
    {
        var candidate = new SkillCandidate("my-skill", "desc", "https://example.com", "content", null, null);
        var evaluation = new SkillEvaluation(candidate, 8, 9, "Great fit", "Very safe", "install", false, null);

        var question = ApproveSkillsHandler.FormatApprovalQuestion(evaluation);

        question.Should().Contain("my-skill");
        question.Should().Contain("8/10");
        question.Should().Contain("9/10");
        question.Should().Contain("Great fit");
        question.Should().Contain("Very safe");
        question.Should().Contain("install");
    }

    #endregion

    #region InstallSkillsHandler

    [Fact]
    public async Task InstallSkills_CopiesFiles()
    {
        // Setup: create a draft directory with skill files
        var draftDir = Path.Combine(_tempDir, "drafts");
        var skillDraftDir = Path.Combine(draftDir, "new-skill");
        Directory.CreateDirectory(skillDraftDir);
        File.WriteAllText(Path.Combine(skillDraftDir, "SKILL.md"), "# New Skill");
        File.WriteAllText(Path.Combine(skillDraftDir, "agentsmith.md"), "metadata");
        File.WriteAllText(Path.Combine(skillDraftDir, "source.md"), "provenance");

        var installDir = Path.Combine(_tempDir, "installed");
        var candidate = new SkillCandidate("new-skill", "desc", "url", "content", null, null);
        var evaluation = new SkillEvaluation(candidate, 8, 9, "fit", "safe", "install", false, null);

        var handler = new InstallSkillsHandler(NullLogger<InstallSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new InstallSkillsContext([evaluation], draftDir, installDir, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("1 skills installed");

        var targetDir = Path.Combine(installDir, "new-skill");
        Directory.Exists(targetDir).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "SKILL.md")).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "agentsmith.md")).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "source.md")).Should().BeTrue();
        File.ReadAllText(Path.Combine(targetDir, "SKILL.md")).Should().Be("# New Skill");
    }

    [Fact]
    public async Task InstallSkills_NoApproved_Skips()
    {
        var handler = new InstallSkillsHandler(NullLogger<InstallSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new InstallSkillsContext([], _tempDir, _tempDir, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("No skills to install");
    }

    [Fact]
    public async Task InstallSkills_MissingDraftDir_Fails()
    {
        var candidate = new SkillCandidate("test", "desc", "url", "content", null, null);
        var evaluation = new SkillEvaluation(candidate, 8, 9, "fit", "safe", "install", false, null);

        var handler = new InstallSkillsHandler(NullLogger<InstallSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new InstallSkillsContext(
            [evaluation], Path.Combine(_tempDir, "nonexistent"), _tempDir, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Draft directory not found");
    }

    [Fact]
    public async Task InstallSkills_MultipleSkills_CopiesAll()
    {
        var draftDir = Path.Combine(_tempDir, "drafts");

        // Create two skill drafts
        foreach (var name in new[] { "skill-a", "skill-b" })
        {
            var dir = Path.Combine(draftDir, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), $"# {name}");
        }

        var installDir = Path.Combine(_tempDir, "installed");
        var skills = new[] { "skill-a", "skill-b" }
            .Select(n => new SkillEvaluation(
                new SkillCandidate(n, "desc", "url", "content", null, null),
                8, 9, "fit", "safe", "install", false, null))
            .ToList();

        var handler = new InstallSkillsHandler(NullLogger<InstallSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var context = new InstallSkillsContext(skills, draftDir, installDir, pipeline);

        var result = await handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("2 skills installed");
        Directory.Exists(Path.Combine(installDir, "skill-a")).Should().BeTrue();
        Directory.Exists(Path.Combine(installDir, "skill-b")).Should().BeTrue();
    }

    [Fact]
    public void CopyDirectory_CopiesRecursively()
    {
        var sourceDir = Path.Combine(_tempDir, "src");
        var subDir = Path.Combine(sourceDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(sourceDir, "root.txt"), "root");
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");

        var targetDir = Path.Combine(_tempDir, "target");
        InstallSkillsHandler.CopyDirectory(sourceDir, targetDir);

        File.Exists(Path.Combine(targetDir, "root.txt")).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "sub", "nested.txt")).Should().BeTrue();
        File.ReadAllText(Path.Combine(targetDir, "root.txt")).Should().Be("root");
        File.ReadAllText(Path.Combine(targetDir, "sub", "nested.txt")).Should().Be("nested");
    }

    #endregion

    #region CommandNames Labels

    [Theory]
    [InlineData(CommandNames.DiscoverSkills, "Discovering skill candidates")]
    [InlineData(CommandNames.EvaluateSkills, "Evaluating skill candidates")]
    [InlineData(CommandNames.DraftSkillFiles, "Drafting skill files")]
    [InlineData(CommandNames.ApproveSkills, "Awaiting skill approval")]
    [InlineData(CommandNames.InstallSkills, "Installing approved skills")]
    public void CommandNames_HaveLabels(string commandName, string expectedLabel)
    {
        CommandNames.GetLabel(commandName).Should().Be(expectedLabel);
    }

    #endregion

    private static ApproveSkillsHandler CreateApproveHandler(bool approves)
    {
        var reporter = new Mock<IProgressReporter>();
        reporter.Setup(r => r.AskYesNoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(approves);

        return new ApproveSkillsHandler(
            Mock.Of<IDialogueTransport>(),
            Mock.Of<IDialogueTrail>(),
            reporter.Object,
            NullLogger<ApproveSkillsHandler>.Instance);
    }
}
