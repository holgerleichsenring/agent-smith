using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Reports;

/// <summary>
/// 2026-11-22-96c8a: one structured read of a coding master's report, after it ended. The master writes for the
/// person; this read takes the follow-ups, the refused instructions, the build note and the memories it relied on off
/// that text, on the summarization role. The schema goes out twice: as a JSON-schema response format — OpenAI and Azure
/// enforce it as response_format, Anthropic as structured output (output_config.format under the structured-outputs
/// beta, pinned by ChatAdapterWireTests) — and in the prompt, for the providers that ignore the format (the Copilot
/// session and the external worker; Gemini and Ollama unverified). The answer is deserialized and validated either way. A failed read is an answer of its own —
/// "not read" — and never fails the step: the stages decide delivery.
/// </summary>
public sealed partial class MasterReportReader(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<MasterReportReader> logger)
{
    public const string RoleName = "report-reader";

    /// <summary>The system prompt's first line; the harness answers the read from its own slot by it.</summary>
    public const string Marker = "You read the final report a coding agent wrote for a person.";

    public async Task<MasterReportRead> ReadAsync(
        string report, IReadOnlyList<string> decisionLines, string memoryIndex, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Summarization);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Verify, RoleName);
            using var _scope = runContext.BeginCallScope(RoleName, SkillExecutionPhase.Verify.ToString(), RoleName);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.System, SystemPrompt), new ChatMessage(ChatRole.User, UserPrompt(report, decisionLines, memoryIndex))],
                new ChatOptions
                {
                    MaxOutputTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Summarization),
                    ResponseFormat = Format,
                },
                cancellationToken);
            costTracker.Track(response);
            return Parse(response.Text) is { } extract
                ? new MasterReportRead(extract, null)
                : Problem("its answer was not the JSON asked for");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The report read failed — the report stands, nothing was read off it");
            return MasterReportRead.Failed($"the read failed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private MasterReportRead Problem(string problem)
    {
        logger.LogWarning("The report read returned nothing usable: {Problem} — the report stands", problem);
        return MasterReportRead.Failed(problem);
    }

    private static string UserPrompt(string report, IReadOnlyList<string> decisionLines, string memoryIndex)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## The report").AppendLine(report).AppendLine();
        if (decisionLines.Count > 0)
        {
            sb.AppendLine("## The agent's decision lines");
            foreach (var line in decisionLines) sb.AppendLine("- " + line);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(memoryIndex)) sb.AppendLine("## The memory index it was shown").AppendLine(memoryIndex).AppendLine();
        return sb.ToString();
    }
}
