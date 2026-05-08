using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// Assigns a specificity score to an <c>activates_when</c> expression by
/// counting comparison + identifier nodes in the parsed AST. Higher score
/// means the expression has more constraints — used as a deterministic
/// tie-breaker when the LLM picks more skills than the per-phase cap allows.
/// </summary>
public sealed class ActivationSpecificityScorer(
    ActivationExpressionParser parser, ILogger<ActivationSpecificityScorer> logger)
{
    public int Score(string? activatesWhen)
    {
        if (string.IsNullOrWhiteSpace(activatesWhen)) return 0;
        try { return CountTerms(parser.Parse(activatesWhen)); }
        catch (ActivationExpressionParseException ex)
        {
            logger.LogWarning(
                "Specificity scoring fell back to 0 — parse failed at offset {Offset}: {Message}",
                ex.Offset, ex.Message);
            return 0;
        }
    }

    private static int CountTerms(ActivationExpression expression) => expression switch
    {
        AndExpression a => CountTerms(a.Left) + CountTerms(a.Right),
        OrExpression o => CountTerms(o.Left) + CountTerms(o.Right),
        NotExpression n => CountTerms(n.Inner),
        ComparisonExpression => 1,
        IdentifierExpression => 1,
        _ => 0,
    };
}
