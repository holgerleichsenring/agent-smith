using Xunit;

namespace Fixture.Tests;

/// <summary>
/// Two tests with controllable outcome so the harness can exercise both
/// green-path and red-path pipeline behaviour. FIXTURE_FAIL=1 flips the
/// second test from pass to fail without recompiling.
/// </summary>
public class CalculatorTests
{
    [Fact]
    public void Add_ReturnsSum()
    {
        Assert.Equal(5, Calculator.Add(2, 3));
    }

    [Fact]
    public void Add_ControllableOutcome_DrivenByEnvVar()
    {
        var shouldFail = Environment.GetEnvironmentVariable("FIXTURE_FAIL") == "1";
        if (shouldFail)
            Assert.Equal(99, Calculator.Add(2, 3));
        else
            Assert.Equal(5, Calculator.Add(2, 3));
    }
}
