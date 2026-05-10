using AgentSmith.Application.Services.Activation;
using AgentSmith.Cli.Commands;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Cli;

public sealed class ValidateConceptsCommandNewFormatTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConceptVocabularyLoader _vocabularyLoader = new(NullLogger<ConceptVocabularyLoader>.Instance);
    private readonly Mock<ISkillLoader> _skillLoaderMock = new();
    private readonly ActivationExpressionParser _parser = new(new ActivationExpressionTokenizer());

    public ValidateConceptsCommandNewFormatTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "validate-concepts-newfmt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        WriteVocabulary("""
            concepts:
              - name: source_available
                type: bool
                description: "x"
                writers: []
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Validate_NewFormatSkillVerifyHintWithoutCategory_ExitOneAndPrintsRule()
    {
        SetupSkills(new RoleSkillDefinition
        {
            Name = "verify-skill",
            Role = "investigator",
            InvestigatorMode = "verify_hint",
            ActivatesWhen = "source_available",
            OutputSchema = "observation",
        });

        var result = SutWith().Validate(_tempDir);

        result.ExitCode.Should().Be(1);
        result.Errors.Should().Contain(e => e.Subject == "verify-skill" && e.Concept == "category");
    }

    [Fact]
    public void Validate_NewFormatSkillJudgeWithoutBlockCondition_ExitOneAndPrintsRule()
    {
        SetupSkills(new RoleSkillDefinition
        {
            Name = "judge-skill",
            Role = "judge",
            ActivatesWhen = "source_available",
            OutputSchema = "observation",
        });

        var result = SutWith().Validate(_tempDir);

        result.ExitCode.Should().Be(1);
        result.Errors.Should().Contain(e => e.Subject == "judge-skill" && e.Concept == "block_condition");
    }

    [Fact]
    public void Validate_NewFormatSkillBootstrapWithNonProducerRole_ExitOneAndPrintsRule()
    {
        SetupSkills(new RoleSkillDefinition
        {
            Name = "bootstrap-skill",
            Role = "judge",
            BlockCondition = "always",
            ActivatesWhen = "source_available",
            OutputSchema = "bootstrap",
        });

        var result = SutWith().Validate(_tempDir);

        result.ExitCode.Should().Be(1);
        result.Errors.Should().Contain(e => e.Subject == "bootstrap-skill" && e.Concept == "output_schema");
    }

    [Fact]
    public void Validate_NewFormatSkillAllRulesSatisfied_ExitZero()
    {
        SetupSkills(new RoleSkillDefinition
        {
            Name = "good",
            Role = "investigator",
            InvestigatorMode = "verify_hint",
            Category = "auth",
            ActivatesWhen = "source_available",
            OutputSchema = "observation",
        });

        SutWith().Validate(_tempDir).ExitCode.Should().Be(0);
    }

    [Fact]
    public void Validate_LegacySkillUnaffectedByNewRules_ExitZero()
    {
        SetupSkills(new RoleSkillDefinition
        {
            Name = "legacy",
            ActivatesWhen = "source_available",
        });

        SutWith().Validate(_tempDir).ExitCode.Should().Be(0);
    }

    private void WriteVocabulary(string yaml) =>
        File.WriteAllText(Path.Combine(_tempDir, "concept-vocabulary.yaml"), yaml);

    private void SetupSkills(params RoleSkillDefinition[] skills) =>
        _skillLoaderMock.Setup(s => s.LoadRoleDefinitions(It.IsAny<string>())).Returns(skills);

    private ValidateConceptsCommand SutWith(params IConceptWriter[] writers) => new(
        _vocabularyLoader,
        _skillLoaderMock.Object,
        _parser,
        new ConceptWriterRegistry(writers));
}
