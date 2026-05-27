using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

public sealed class ConceptVocabularyLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConceptVocabularyLoader _loader;

    public ConceptVocabularyLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-vocab-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _loader = new ConceptVocabularyLoader(
            new NoOpEventPublisher(),
            new AsyncLocalRunContextAccessor(), new NoOpSystemEventPublisher(), NullLogger<ConceptVocabularyLoader>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void LoadValidFlatYaml_LoadsAllEntries()
    {
        WriteVocabulary("""
            concepts:
              - name: persistence
                type: bool
                description: "Project stores state"
                writers: []
              - name: findings_count
                type: int
                description: "Number of findings"
                int_range: [0, 10000]
                writers: [SpawnNucleiHandler]
              - name: project_language
                type: enum
                description: "Primary project language"
                enum_values: [csharp, node, python]
                writers: [ProjectAnalyzer]
            """);

        var vocab = _loader.Load(_tempDir);

        vocab.Concepts.Should().HaveCount(3);
        vocab.TryGet("persistence", out var p).Should().BeTrue();
        p.Type.Should().Be(ConceptType.Bool);
        p.Description.Should().Be("Project stores state");
        p.Writers.Should().BeEmpty();

        vocab.TryGet("findings_count", out var f).Should().BeTrue();
        f.Type.Should().Be(ConceptType.Int);
        f.IntRange.Should().Be(new ConceptIntRange(0, 10000));
        f.Writers.Should().ContainSingle().Which.Should().Be("SpawnNucleiHandler");

        vocab.TryGet("project_language", out var l).Should().BeTrue();
        l.Type.Should().Be(ConceptType.Enum);
        l.EnumValues.Should().Equal("csharp", "node", "python");
    }

    [Fact]
    public void LoadValidYaml_BoolDefaultsToFalse()
    {
        WriteVocabulary("""
            concepts:
              - name: flag
                type: bool
                description: "A flag"
            """);
        var vocab = _loader.Load(_tempDir);

        vocab.GetDefault("flag").Should().Be(false);
    }

    [Fact]
    public void LoadValidYaml_IntDefaultsToZero()
    {
        WriteVocabulary("""
            concepts:
              - name: count
                type: int
                description: "A count"
                int_range: [0, 100]
            """);
        var vocab = _loader.Load(_tempDir);

        vocab.GetDefault("count").Should().Be(0);
    }

    [Fact]
    public void LoadValidYaml_EnumDefaultsToFirstValue()
    {
        WriteVocabulary("""
            concepts:
              - name: kind
                type: enum
                description: "A kind"
                enum_values: [first, second, third]
            """);
        var vocab = _loader.Load(_tempDir);

        vocab.GetDefault("kind").Should().Be("first");
    }

    [Fact]
    public void LoadLegacyThreeSectionShape_FailsWithMigrationHint()
    {
        WriteVocabulary("""
            concepts:
              project_concepts:
                - {key: persistence, desc: "Project stores state"}
              change_signals:
                - {key: pattern_decision, desc: "Pattern decision"}
              run_context:
                - {key: scan_context, desc: "Active scan pipeline"}
            """);

        var act = () => _loader.Load(_tempDir);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*legacy three-section shape*");
    }

    [Fact]
    public void LoadEnumWithoutValues_Fails()
    {
        WriteVocabulary("""
            concepts:
              - name: kind
                type: enum
                description: "A kind"
            """);

        var act = () => _loader.Load(_tempDir);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*enum concept 'kind' must declare a non-empty 'enum_values'*");
    }

    [Fact]
    public void LoadIntWithEnumValues_Fails()
    {
        WriteVocabulary("""
            concepts:
              - name: bogus
                type: int
                description: "Bad"
                int_range: [0, 10]
                enum_values: [a, b]
            """);

        var act = () => _loader.Load(_tempDir);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*'bogus' has 'enum_values'*type is 'int'*");
    }

    [Fact]
    public void LoadBoolWithIntRange_Fails()
    {
        WriteVocabulary("""
            concepts:
              - name: bogus
                type: bool
                description: "Bad"
                int_range: [0, 10]
            """);

        var act = () => _loader.Load(_tempDir);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*'bogus' has 'int_range'*type is 'bool'*");
    }

    [Fact]
    public void LoadEntryMissingType_Fails()
    {
        WriteVocabulary("""
            concepts:
              - name: missing_type
                description: "No type declared"
            """);

        var act = () => _loader.Load(_tempDir);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*'missing_type' is missing 'type'*");
    }

    [Fact]
    public void LoadEntryMissingName_Fails()
    {
        WriteVocabulary("""
            concepts:
              - type: bool
                description: "No name"
            """);

        var act = () => _loader.Load(_tempDir);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*entry is missing 'name'*");
    }

    [Fact]
    public void LoadDuplicateName_Fails()
    {
        WriteVocabulary("""
            concepts:
              - name: shared
                type: bool
                description: "First"
              - name: shared
                type: bool
                description: "Second"
            """);

        var act = () => _loader.Load(_tempDir);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*duplicate concept name 'shared'*");
    }

    [Fact]
    public void LoadMissingFile_ReturnsEmpty()
    {
        var vocab = _loader.Load(_tempDir);
        vocab.Concepts.Should().BeEmpty();
    }

    private void WriteVocabulary(string yaml) =>
        File.WriteAllText(Path.Combine(_tempDir, "concept-vocabulary.yaml"), yaml);
}
