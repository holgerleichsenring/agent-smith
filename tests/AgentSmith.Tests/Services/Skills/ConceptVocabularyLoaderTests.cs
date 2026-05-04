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
        _loader = new ConceptVocabularyLoader(NullLogger<ConceptVocabularyLoader>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void LoadsThreeSections_FlattensIntoNamespace()
    {
        File.WriteAllText(Path.Combine(_tempDir, "concept-vocabulary.yaml"), """
            concepts:
              project_concepts:
                - {key: persistence, desc: "Project stores state"}
                - {key: authentication, desc: "Project verifies identity"}
              change_signals:
                - {key: pattern_decision, desc: "Pattern decision"}
              run_context:
                - {key: scan_context, desc: "Active scan pipeline"}
            """);

        var vocab = _loader.Load(_tempDir);

        vocab.Concepts.Should().HaveCount(4);
        vocab.TryGet("persistence", out var p).Should().BeTrue();
        p.Section.Should().Be("project_concepts");
        vocab.TryGet("pattern_decision", out var ps).Should().BeTrue();
        ps.Section.Should().Be("change_signals");
        vocab.TryGet("scan_context", out var rc).Should().BeTrue();
        rc.Section.Should().Be("run_context");
    }

    [Fact]
    public void DuplicateKeyAcrossSections_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "concept-vocabulary.yaml"), """
            concepts:
              project_concepts:
                - {key: shared_key, desc: "First"}
              change_signals:
                - {key: shared_key, desc: "Second"}
            """);

        var act = () => _loader.Load(_tempDir);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*shared_key*project_concepts*change_signals*");
    }

    [Fact]
    public void MissingFile_ReturnsEmpty()
    {
        var vocab = _loader.Load(_tempDir);
        vocab.Concepts.Should().BeEmpty();
    }
}
