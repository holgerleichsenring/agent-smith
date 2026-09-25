namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Binary comparison node (e.g. <c>findings_count &gt; 0</c>, <c>pipeline_name = "code"</c>).
/// </summary>
public sealed record ComparisonExpression(
    ActivationExpression Left,
    ComparisonOperator Operator,
    ActivationExpression Right) : ActivationExpression;
