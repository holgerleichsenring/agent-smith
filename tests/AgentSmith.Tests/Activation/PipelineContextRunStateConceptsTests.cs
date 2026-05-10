using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Infrastructure.Services.Activation;
using FluentAssertions;

namespace AgentSmith.Tests.Activation;

public sealed class PipelineContextRunStateConceptsTests
{
    [Fact]
    public void SetBool_ValidConcept_StoresValue()
    {
        var sut = StateWith(BoolConcept("flag"));

        sut.SetBool("flag", true);

        sut.GetBool("flag").Should().BeTrue();
    }

    [Fact]
    public void SetBool_OnIntConcept_Throws()
    {
        var sut = StateWith(IntConcept("count"));

        var act = () => sut.SetBool("count", true);

        act.Should().Throw<ConceptTypeMismatchException>()
            .Which.DeclaredType.Should().Be(ConceptType.Int);
    }

    [Fact]
    public void SetInt_OutsideDeclaredRange_Throws()
    {
        var sut = StateWith(IntConceptWithRange("severity", 0, 5));

        var act = () => sut.SetInt("severity", 10);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetEnum_UnknownValue_Throws()
    {
        var sut = StateWith(EnumConcept("color", "red", "green", "blue"));

        var act = () => sut.SetEnum("color", "yellow");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetBool_UnsetConcept_ReturnsTypedDefault()
    {
        var sut = StateWith(BoolConcept("flag"));

        sut.GetBool("flag").Should().BeFalse();
    }

    [Fact]
    public void GetEnum_UnsetConcept_ReturnsFirstEnumValue()
    {
        var sut = StateWith(EnumConcept("color", "red", "green", "blue"));

        sut.GetEnum("color").Should().Be("red");
    }

    [Fact]
    public void SetBool_UndeclaredConcept_Throws()
    {
        var sut = StateWith();

        var act = () => sut.SetBool("undeclared", true);

        act.Should().Throw<KeyNotFoundException>();
    }

    private static IRunStateConcepts StateWith(params ProjectConcept[] concepts)
    {
        var dict = concepts.ToDictionary(c => c.Name);
        return new PipelineContextRunStateConcepts(new PipelineContext(), new ConceptVocabulary(dict));
    }

    private static ProjectConcept BoolConcept(string name) =>
        new(name, "test", ConceptType.Bool, null, null, []);

    private static ProjectConcept IntConcept(string name) =>
        new(name, "test", ConceptType.Int, null, new ConceptIntRange(0, 1000), []);

    private static ProjectConcept IntConceptWithRange(string name, int min, int max) =>
        new(name, "test", ConceptType.Int, null, new ConceptIntRange(min, max), []);

    private static ProjectConcept EnumConcept(string name, params string[] values) =>
        new(name, "test", ConceptType.Enum, values, null, []);
}
