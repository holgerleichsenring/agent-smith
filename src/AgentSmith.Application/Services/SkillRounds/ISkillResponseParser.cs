using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Parses skill-round LLM responses into observations + applies the
/// confidence threshold + renders observations as discussion-log text. Wraps
/// <c>ObservationParser</c> with the round-specific post-processing the old
/// SkillRoundHandlerBase used to do inline (and that VerifyRoundHandler
/// duplicated).
/// </summary>
public interface ISkillResponseParser
{
    List<SkillObservation> ParseAndDowngrade(
        string responseText, string skillName, ILogger logger,
        IReadOnlyCollection<string>? readPaths = null);

    string RenderObservationsAsText(IReadOnlyList<SkillObservation> observations);
}
