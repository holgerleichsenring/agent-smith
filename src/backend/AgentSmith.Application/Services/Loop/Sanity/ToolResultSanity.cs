using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// Runs the sanity rules over one tool result, in their registered order, and returns what they
/// observed. A rule that throws is logged and skipped: an observation is a sentence, never a
/// reason for a tool call to fail.
/// </summary>
public sealed class ToolResultSanity(IEnumerable<IToolResultSanityRule> rules, ILogger<ToolResultSanity> logger)
{
    /// <summary>The head of every observation line appended to a tool result.</summary>
    public const string Prefix = "[framework] ";

    /// <summary>The filesystem host's own failure test: a result starting with "Error".</summary>
    public static bool IsError(string text) => text.StartsWith("Error", StringComparison.Ordinal);

    public IReadOnlyList<string> Observe(ToolResultSanityCall call, ToolResultSanityScope scope)
    {
        var observations = new List<string>();
        foreach (var rule in rules)
        {
            try
            {
                if (rule.Observe(call, scope) is { } observation) observations.Add(observation);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sanity rule {Rule} failed on {Tool}; the result passes unannotated by it",
                    rule.GetType().Name, call.Tool);
            }
        }
        return observations;
    }
}
