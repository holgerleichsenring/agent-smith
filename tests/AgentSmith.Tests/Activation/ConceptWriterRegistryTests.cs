using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Models.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Activation;

public sealed class ConceptWriterRegistryTests
{
    [Fact]
    public void Build_NoWriters_BuildsEmptyRegistry()
    {
        var sut = new ConceptWriterRegistry([]);

        sut.ConceptToHandlers.Should().BeEmpty();
        sut.Writers.Should().BeEmpty();
    }

    [Fact]
    public void Build_SingleWriterSingleConcept_BuildsRegistry()
    {
        var sut = new ConceptWriterRegistry([
            new TestWriterA(new ConceptDeclaration("source_available", ConceptType.Bool))
        ]);

        sut.ConceptToHandlers.Should().ContainKey("source_available");
        sut.ConceptToHandlers["source_available"].Should().HaveCount(1);
        sut.ConceptToHandlers["source_available"][0].HandlerClassName.Should().Be("TestWriterA");
        sut.ConceptToHandlers["source_available"][0].DeclaredType.Should().Be(ConceptType.Bool);
        sut.Writers.Should().HaveCount(1);
    }

    [Fact]
    public void Build_MultipleWritersSameConcept_GroupsHandlersByConcept()
    {
        var sut = new ConceptWriterRegistry([
            new TestWriterA(new ConceptDeclaration("source_available", ConceptType.Bool)),
            new TestWriterB(new ConceptDeclaration("source_available", ConceptType.Bool))
        ]);

        sut.ConceptToHandlers["source_available"].Should().HaveCount(2);
        sut.ConceptToHandlers["source_available"]
            .Select(h => h.HandlerClassName).Should()
            .BeEquivalentTo(new[] { "TestWriterA", "TestWriterB" });
    }

    [Fact]
    public void Build_HandlerDeclaresMultipleConcepts_AllListed()
    {
        var sut = new ConceptWriterRegistry([
            new TestWriterA(
                new ConceptDeclaration("context_yaml_present", ConceptType.Bool),
                new ConceptDeclaration("coding_principles_present", ConceptType.Bool))
        ]);

        sut.ConceptToHandlers.Should().ContainKeys("context_yaml_present", "coding_principles_present");
        sut.Writers.Should().HaveCount(1);
        sut.Writers[0].DeclaredConcepts.Should().HaveCount(2);
    }

    private sealed class TestWriterA : IConceptWriter
    {
        public TestWriterA(params ConceptDeclaration[] decls) => DeclaredConcepts = decls;
        public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; }
    }

    private sealed class TestWriterB : IConceptWriter
    {
        public TestWriterB(params ConceptDeclaration[] decls) => DeclaredConcepts = decls;
        public IReadOnlyList<ConceptDeclaration> DeclaredConcepts { get; }
    }
}
