using AgentSmith.Application.Services.Activation;
using AgentSmith.Cli.Commands;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Cli;

public sealed class ValidateConceptsCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConceptVocabularyLoader _vocabularyLoader = new(NullLogger<ConceptVocabularyLoader>.Instance);
    private readonly Mock<ISkillLoader> _skillLoaderMock = new();
    private readonly ActivationExpressionParser _parser = new(new ActivationExpressionTokenizer());

    public ValidateConceptsCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "validate-concepts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Validate_AllSkillsClean_ExitZero()
    {
        WriteVocabulary("""
            concepts:
              - name: source_available
                type: bool
                description: "x"
                writers: [TestWriter]
            """);
        SetupSkills(new RoleSkillDefinition { Name = "skill-a", ActivatesWhen = "source_available" });

        var sut = SutWith(new TestWriter("source_available", ConceptType.Bool));
        var result = sut.Validate(_tempDir);

        result.ExitCode.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NoActivatesWhenInAnySkill_StillPassesWriterRegistry()
    {
        WriteVocabulary("""
            concepts:
              - name: source_available
                type: bool
                description: "x"
                writers: [TestWriter]
            """);
        SetupSkills(new RoleSkillDefinition { Name = "skill-a", ActivatesWhen = null });

        var sut = SutWith(new TestWriter("source_available", ConceptType.Bool));
        var result = sut.Validate(_tempDir);

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public void Validate_UnknownConceptInActivatesWhen_ExitOneAndPrintsSkill()
    {
        WriteVocabulary("""
            concepts:
              - name: source_available
                type: bool
                description: "x"
                writers: []
            """);
        SetupSkills(new RoleSkillDefinition { Name = "skill-a", ActivatesWhen = "totally_made_up" });

        var sut = SutWith();
        var result = sut.Validate(_tempDir);

        result.ExitCode.Should().Be(1);
        result.Errors.Should().Contain(e => e.Subject == "skill-a" && e.Concept == "totally_made_up");
    }

    [Fact]
    public void Validate_HandlerWritesUndeclaredConcept_ExitOneAndPrintsHandler()
    {
        WriteVocabulary("""
            concepts:
              - name: source_available
                type: bool
                description: "x"
                writers: []
            """);
        SetupSkills();

        var sut = SutWith(new TestWriter("undeclared_concept", ConceptType.Bool));
        var result = sut.Validate(_tempDir);

        result.ExitCode.Should().Be(1);
        result.Errors.Should().Contain(e =>
            e.Subject == "TestWriter" && e.Concept == "undeclared_concept" &&
            e.Message.Contains("not present in vocabulary"));
    }

    [Fact]
    public void Validate_VocabularyEntryWithoutBackingHandler_ExitOneAndPrintsConcept()
    {
        WriteVocabulary("""
            concepts:
              - name: source_available
                type: bool
                description: "x"
                writers: [GhostHandler]
            """);
        SetupSkills();

        var sut = SutWith();
        var result = sut.Validate(_tempDir);

        result.ExitCode.Should().Be(1);
        result.Errors.Should().Contain(e =>
            e.Subject == "GhostHandler" && e.Concept == "source_available" &&
            e.Message.Contains("not backed by a registered IConceptWriter"));
    }

    [Fact]
    public void Validate_HandlerDeclaresWrongType_ExitOneAndPrintsTypeMismatch()
    {
        WriteVocabulary("""
            concepts:
              - name: source_available
                type: bool
                description: "x"
                writers: [TestWriter]
            """);
        SetupSkills();

        var sut = SutWith(new TestWriter("source_available", ConceptType.Int));
        var result = sut.Validate(_tempDir);

        result.ExitCode.Should().Be(1);
        result.Errors.Should().Contain(e =>
            e.Concept == "source_available" && e.Message.Contains("type Int") && e.Message.Contains("declares Bool"));
    }

    [Fact]
    public void Validate_MalformedActivatesWhenExpression_ExitOneAndPrintsParseError()
    {
        WriteVocabulary("""
            concepts:
              - name: source_available
                type: bool
                description: "x"
                writers: []
            """);
        SetupSkills(new RoleSkillDefinition { Name = "skill-a", ActivatesWhen = "source_available AND AND" });

        var sut = SutWith();
        var result = sut.Validate(_tempDir);

        result.ExitCode.Should().Be(1);
        result.Errors.Should().Contain(e =>
            e.Subject == "skill-a" && e.Concept == "<parse>" &&
            e.Message.Contains("parse error at offset"));
    }

    [Fact]
    public void Validate_EmptyWritersList_NoErrorWithoutHandler()
    {
        WriteVocabulary("""
            concepts:
              - name: orphan_concept
                type: bool
                description: "no writer required"
                writers: []
            """);
        SetupSkills();

        var sut = SutWith();
        var result = sut.Validate(_tempDir);

        result.ExitCode.Should().Be(0);
        result.Errors.Should().BeEmpty();
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

    private sealed class TestWriter : IConceptWriter
    {
        public TestWriter(string name, ConceptType type) =>
            DeclaredConcepts = [new ConceptDeclaration(name, type)];
        public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; }
    }
}
