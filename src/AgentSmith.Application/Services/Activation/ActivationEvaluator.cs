using AgentSmith.Contracts.Activation;

namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Evaluates an <see cref="ActivationExpression"/> AST against an
/// <see cref="IRunStateConcepts"/> snapshot, returning a single boolean. Boolean
/// connectives short-circuit; comparisons infer their type from a literal operand
/// (string equality, int ordering, bool equality). Strings and bools allow only
/// equality; ordered comparisons throw <see cref="ActivationExpressionEvaluateException"/>.
/// </summary>
public sealed class ActivationEvaluator
{
    public bool Evaluate(ActivationExpression expression, IRunStateConcepts state) =>
        expression switch
        {
            AndExpression a => Evaluate(a.Left, state) && Evaluate(a.Right, state),
            OrExpression o => Evaluate(o.Left, state) || Evaluate(o.Right, state),
            NotExpression n => !Evaluate(n.Inner, state),
            BoolLiteralExpression b => b.Value,
            IdentifierExpression id => state.GetBool(id.Name),
            ComparisonExpression c => EvaluateComparison(c, state),
            _ => throw new ActivationExpressionEvaluateException(
                $"Expression type {expression.GetType().Name} is not valid in boolean position.")
        };

    private static bool EvaluateComparison(ComparisonExpression c, IRunStateConcepts state)
    {
        if (HasInt(c))
            return CompareInts(ResolveInt(c.Left, state), c.Operator, ResolveInt(c.Right, state));
        if (HasString(c))
            return CompareStrings(ResolveString(c.Left, state), c.Operator, ResolveString(c.Right, state));
        if (HasBool(c))
            return CompareBools(ResolveBool(c.Left, state), c.Operator, ResolveBool(c.Right, state));
        throw new ActivationExpressionEvaluateException(
            "Comparison requires at least one literal operand to determine the comparison type.");
    }

    private static bool HasInt(ComparisonExpression c) =>
        c.Left is IntLiteralExpression || c.Right is IntLiteralExpression;

    private static bool HasString(ComparisonExpression c) =>
        c.Left is StringLiteralExpression || c.Right is StringLiteralExpression;

    private static bool HasBool(ComparisonExpression c) =>
        c.Left is BoolLiteralExpression || c.Right is BoolLiteralExpression;

    private static int ResolveInt(ActivationExpression e, IRunStateConcepts state) =>
        e switch
        {
            IntLiteralExpression i => i.Value,
            IdentifierExpression id => state.GetInt(id.Name),
            _ => throw new ActivationExpressionEvaluateException(
                $"Cannot resolve {e.GetType().Name} as an integer.")
        };

    private static string ResolveString(ActivationExpression e, IRunStateConcepts state) =>
        e switch
        {
            StringLiteralExpression s => s.Value,
            IdentifierExpression id => state.GetEnum(id.Name),
            _ => throw new ActivationExpressionEvaluateException(
                $"Cannot resolve {e.GetType().Name} as a string.")
        };

    private static bool ResolveBool(ActivationExpression e, IRunStateConcepts state) =>
        e switch
        {
            BoolLiteralExpression b => b.Value,
            IdentifierExpression id => state.GetBool(id.Name),
            _ => throw new ActivationExpressionEvaluateException(
                $"Cannot resolve {e.GetType().Name} as a bool.")
        };

    private static bool CompareInts(int l, ComparisonOperator op, int r) =>
        op switch
        {
            ComparisonOperator.Equals => l == r,
            ComparisonOperator.GreaterThan => l > r,
            ComparisonOperator.GreaterOrEqual => l >= r,
            ComparisonOperator.LessThan => l < r,
            ComparisonOperator.LessOrEqual => l <= r,
            _ => throw new ActivationExpressionEvaluateException($"Unknown comparison operator {op}.")
        };

    private static bool CompareStrings(string l, ComparisonOperator op, string r) =>
        op == ComparisonOperator.Equals
            ? string.Equals(l, r, StringComparison.Ordinal)
            : throw new ActivationExpressionEvaluateException(
                $"Operator {op} is not allowed on string/enum operands; only Equals is supported.");

    private static bool CompareBools(bool l, ComparisonOperator op, bool r) =>
        op == ComparisonOperator.Equals
            ? l == r
            : throw new ActivationExpressionEvaluateException(
                $"Operator {op} is not allowed on bool operands; only Equals is supported.");
}
