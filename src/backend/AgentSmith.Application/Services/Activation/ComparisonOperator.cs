namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Comparison operator carried by <see cref="ComparisonExpression"/>. Ordered
/// operators (greater/less variants) are valid only on int operands; strings and
/// bools support only <see cref="Equals"/>.
/// </summary>
public enum ComparisonOperator
{
    Equals,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual
}
