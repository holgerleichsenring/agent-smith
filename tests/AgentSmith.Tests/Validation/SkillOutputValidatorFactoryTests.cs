using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

public sealed class SkillOutputValidatorFactoryTests
{
    private static SkillOutputValidatorFactory NewFactory()
    {
        var loader = new JsonSchemaLoader();
        return new SkillOutputValidatorFactory(
            new PlanOutputValidator(loader),
            new DiffOutputValidator(loader),
            new BootstrapOutputValidator(loader),
            new ObservationOutputValidator(TolerantJsonParserFactory.CreateObservation()),
            new DiscoveryOutputValidator(loader),
            new NoOpSkillOutputValidator());
    }

    [Fact]
    public void ForSchema_Discovery_ReturnsDiscoveryValidator()
        => NewFactory().ForSchema("discovery").Should().BeOfType<DiscoveryOutputValidator>();

    [Fact]
    public void ForSchema_Plan_ReturnsPlanValidator()
        => NewFactory().ForSchema("plan").Should().BeOfType<PlanOutputValidator>();

    [Fact]
    public void ForSchema_Diff_ReturnsDiffValidator()
        => NewFactory().ForSchema("diff").Should().BeOfType<DiffOutputValidator>();

    [Fact]
    public void ForSchema_Bootstrap_ReturnsBootstrapValidator()
        => NewFactory().ForSchema("bootstrap").Should().BeOfType<BootstrapOutputValidator>();

    [Fact]
    public void ForSchema_Observation_ReturnsObservationValidator()
        => NewFactory().ForSchema("observation").Should().BeOfType<ObservationOutputValidator>();

    [Fact]
    public void ForSchema_Null_ReturnsNoOpValidator()
        => NewFactory().ForSchema(null).Should().BeOfType<NoOpSkillOutputValidator>();

    [Fact]
    public void ForSchema_Empty_ReturnsNoOpValidator()
        => NewFactory().ForSchema("").Should().BeOfType<NoOpSkillOutputValidator>();

    [Fact]
    public void ForSchema_UnknownString_Throws()
    {
        var act = () => NewFactory().ForSchema("invalid");

        act.Should().Throw<ArgumentException>().WithMessage("*invalid*");
    }
}
