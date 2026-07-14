using Xunit;

namespace Sample.Tests;

/// <summary>
/// Pins the bulk-discount rule: orders of exactly the threshold amount
/// (100.00) qualify for the 10% discount. The boundary test fails against
/// the shipped implementation — that failure is the demo's seeded bug.
/// </summary>
public class PriceCalculatorTests
{
    [Fact]
    public void Total_ExactlyAtBulkThreshold_GetsDiscount()
    {
        Assert.Equal(90m, PriceCalculator.Total(100m));
    }

    [Fact]
    public void Total_BelowThreshold_NoDiscount()
    {
        Assert.Equal(50m, PriceCalculator.Total(50m));
    }

    [Fact]
    public void Total_AboveThreshold_GetsDiscount()
    {
        Assert.Equal(180m, PriceCalculator.Total(200m));
    }
}
