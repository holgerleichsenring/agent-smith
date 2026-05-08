using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Infrastructure.Services.Activation;
using FluentAssertions;

namespace AgentSmith.Tests.Activation;

public sealed class ActivationEvaluatorTests
{
    private readonly ActivationEvaluator _evaluator = new();
    private readonly ActivationExpressionParser _parser = new(new ActivationExpressionTokenizer());

    [Fact]
    public void Evaluate_SingleBoolConcept_ReturnsValue()
    {
        var state = StateWith(BoolConcept("flag"));
        state.SetBool("flag", true);

        var result = _evaluator.Evaluate(_parser.Parse("flag"), state);

        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_AndShortCircuit_StopsAtFirstFalse()
    {
        // Right side is undeclared — would throw if evaluated. Short-circuit means it isn't.
        var state = StateWith(BoolConcept("a"));

        var result = _evaluator.Evaluate(_parser.Parse("a AND b_undeclared"), state);

        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_OrShortCircuit_StopsAtFirstTrue()
    {
        var state = StateWith(BoolConcept("a"));
        state.SetBool("a", true);

        var result = _evaluator.Evaluate(_parser.Parse("a OR b_undeclared"), state);

        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_MissingBoolConcept_ReturnsFalse()
    {
        var state = StateWith(BoolConcept("flag"));

        var result = _evaluator.Evaluate(_parser.Parse("flag"), state);

        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MissingIntConcept_ReturnsZero()
    {
        var state = StateWith(IntConcept("count"));

        var result = _evaluator.Evaluate(_parser.Parse("count > 0"), state);

        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MissingEnumConcept_ReturnsFirstValue()
    {
        var state = StateWith(EnumConcept("color", "red", "green", "blue"));

        var equalsRed = _evaluator.Evaluate(_parser.Parse("color = \"red\""), state);
        var equalsBlue = _evaluator.Evaluate(_parser.Parse("color = \"blue\""), state);

        equalsRed.Should().BeTrue();
        equalsBlue.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_StringEqualsOnEnum_ReturnsCorrectBool()
    {
        var state = StateWith(EnumConcept("pipeline_name", "fix-bug", "security-scan"));
        state.SetEnum("pipeline_name", "fix-bug");

        var match = _evaluator.Evaluate(_parser.Parse("pipeline_name = \"fix-bug\""), state);
        var noMatch = _evaluator.Evaluate(_parser.Parse("pipeline_name = \"security-scan\""), state);

        match.Should().BeTrue();
        noMatch.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_OrderedComparisonOnEnum_Throws()
    {
        var state = StateWith(EnumConcept("pipeline_name", "fix-bug", "security-scan"));

        var act = () => _evaluator.Evaluate(_parser.Parse("pipeline_name > \"fix-bug\""), state);

        act.Should().Throw<ActivationExpressionEvaluateException>();
    }

    [Fact]
    public void Evaluate_IntComparison_ReturnsCorrectBool()
    {
        var state = StateWith(IntConcept("findings_count"));
        state.SetInt("findings_count", 5);

        var gt = _evaluator.Evaluate(_parser.Parse("findings_count > 3"), state);
        var ge = _evaluator.Evaluate(_parser.Parse("findings_count >= 5"), state);
        var eq = _evaluator.Evaluate(_parser.Parse("findings_count = 5"), state);
        var lt = _evaluator.Evaluate(_parser.Parse("findings_count < 5"), state);

        gt.Should().BeTrue();
        ge.Should().BeTrue();
        eq.Should().BeTrue();
        lt.Should().BeFalse();
    }

    private static IRunStateConcepts StateWith(params ProjectConcept[] concepts)
    {
        var dict = concepts.ToDictionary(c => c.Name);
        var vocabulary = new ConceptVocabulary(dict);
        return new PipelineContextRunStateConcepts(new PipelineContext(), vocabulary);
    }

    private static ProjectConcept BoolConcept(string name) =>
        new(name, "test", ConceptType.Bool, null, null, []);

    private static ProjectConcept IntConcept(string name) =>
        new(name, "test", ConceptType.Int, null, new ConceptIntRange(0, 1000), []);

    private static ProjectConcept EnumConcept(string name, params string[] values) =>
        new(name, "test", ConceptType.Enum, values, null, []);
}
