namespace Sample;

/// <summary>
/// Computes an order total. Orders at or above the bulk threshold receive
/// the bulk discount — see PriceCalculatorTests for the pinned rule.
/// </summary>
public static class PriceCalculator
{
    public const decimal BulkThreshold = 100m;
    public const decimal BulkDiscountRate = 0.10m;

    public static decimal Total(decimal orderAmount)
    {
        if (orderAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(orderAmount));

        return orderAmount > BulkThreshold
            ? orderAmount * (1 - BulkDiscountRate)
            : orderAmount;
    }
}
