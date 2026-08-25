using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.UseCases;

/// <summary>
/// p0515: the shared concept vocabulary, extracted out of ExecutePipelineUseCase. An
/// un-bootstrapped catalog is not an error — the vocabulary is empty and LoadSkills
/// repopulates it later in every preset.
/// </summary>
public sealed class ConceptVocabularyLoaderTests
{
    [Fact]
    public void Load_ACatalogThatIsNotBootstrapped_IsEmptyRatherThanAFailure()
    {
        var loader = new ConceptVocabularyLoader(
            new StubSkillsCatalogPath(Path.Combine(Path.GetTempPath(), "agentsmith-absent-catalog")),
            Mock.Of<ISkillLoader>(),
            NullLogger<ConceptVocabularyLoader>.Instance);

        loader.Load().Should().BeSameAs(ConceptVocabulary.Empty);
    }

    [Fact]
    public void Load_ABootstrappedCatalog_ReadsTheVocabularyFromTheSkillsSubtree()
    {
        var root = Directory.CreateTempSubdirectory("agentsmith-vocab").FullName;
        var skillsRoot = Path.Combine(root, ConceptVocabularyLoader.CatalogSkillsRootSubPath);
        Directory.CreateDirectory(skillsRoot);
        var vocabulary = new ConceptVocabulary(new Dictionary<string, ProjectConcept>());
        var skillLoader = new Mock<ISkillLoader>();
        skillLoader.Setup(s => s.LoadVocabulary(skillsRoot)).Returns(vocabulary);

        var loaded = new ConceptVocabularyLoader(
            new StubSkillsCatalogPath(root), skillLoader.Object,
            NullLogger<ConceptVocabularyLoader>.Instance).Load();

        loaded.Should().BeSameAs(vocabulary);
    }
}
