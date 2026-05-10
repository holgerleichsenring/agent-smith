using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Validation;

public sealed class ObservationOutputValidatorTests
{
    private static ObservationOutputValidator NewValidator() => new();

    [Fact]
    public void Validate_ParsableObservationList_ReturnsOk()
    {
        var result = NewValidator().Validate(PlanFixtures.SingleObservation());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoObservations_ReturnsFailure()
    {
        var result = NewValidator().Validate("[]");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("zero parseable observations");
    }

    [Fact]
    public void Validate_RecoverableTruncatedJson_ReturnsOkViaResilientPath()
    {
        var truncated = """
        [
          {"concern":"correctness","description":"first","blocking":false,"severity":"low","confidence":50},
          {"concern":"correctness","description":"second","blocking":false,"severity
        """;

        var result = NewValidator().Validate(truncated);

        result.IsValid.Should().BeTrue();
    }
}
