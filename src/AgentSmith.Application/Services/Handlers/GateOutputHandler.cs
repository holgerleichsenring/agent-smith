using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses LLM gate output (verdict or finding list) and writes
/// confirmed findings to the pipeline context. Parse failures are
/// surfaced as CommandResult.Fail with the raw response logged.
/// </summary>
public sealed class GateOutputHandler(
    ILogger<GateOutputHandler> logger) : IGateOutputHandler
{
    private const int ResponseLogLimit = 2000;

    public CommandResult Handle(
        RoleSkillDefinition role,
        SkillOrchestration orchestration,
        string responseText,
        PipelineContext pipeline)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            logger.LogError("Gate {Name}: empty LLM response", role.DisplayName);
            return CommandResult.Fail($"Gate {role.DisplayName}: empty LLM response");
        }

        return orchestration.Output == SkillOutputType.Verdict
            ? HandleVerdict(role, responseText)
            : HandleFindingList(role, orchestration, responseText, pipeline);
    }

    private CommandResult HandleVerdict(RoleSkillDefinition role, string responseText)
    {
        try
        {
            using var doc = JsonDocument.Parse(JsonExtractor.Extract(responseText));

            if (!doc.RootElement.TryGetProperty("pass", out var passProp))
                return FailWithLog(role, "missing 'pass' property in verdict response", responseText);

            var pass = passProp.GetBoolean();
            var reason = doc.RootElement.TryGetProperty("reason", out var r)
                ? r.GetString() ?? "" : "";

            return pass
                ? CommandResult.Ok($"Gate {role.DisplayName}: passed")
                : CommandResult.Fail($"Gate veto ({role.DisplayName}): {reason}");
        }
        catch (JsonException ex)
        {
            return FailWithLog(role, $"invalid JSON in verdict response — {ex.Message}", responseText, ex);
        }
    }

    private CommandResult HandleFindingList(
        RoleSkillDefinition role, SkillOrchestration orchestration,
        string responseText, PipelineContext pipeline)
    {
        try
        {
            using var doc = JsonDocument.Parse(JsonExtractor.Extract(responseText));

            if (!doc.RootElement.TryGetProperty("confirmed", out var confirmed))
                return FailWithLog(role, "missing 'confirmed' property in list response", responseText);

            var count = confirmed.GetArrayLength();
            var rejected = doc.RootElement.TryGetProperty("rejected", out var rej)
                ? rej.GetArrayLength() : 0;

            var gateFindings = GateFindingParser.Parse(confirmed);
            var merged = GateFindingMerger.Merge(gateFindings, orchestration, pipeline);
            pipeline.Set(ContextKeys.ExtractedFindings, merged.AsReadOnly());

            LogFindings(role, gateFindings, count, rejected);

            return CommandResult.Ok(
                $"Gate {role.DisplayName}: {count} findings confirmed, {merged.Count} total after merge");
        }
        catch (JsonException ex)
        {
            return FailWithLog(role, $"invalid JSON in list response — {ex.Message}", responseText, ex);
        }
        catch (Exception ex)
        {
            return FailWithLog(role, $"failed to parse list response — {ex.Message}", responseText, ex);
        }
    }

    private CommandResult FailWithLog(
        RoleSkillDefinition role, string reason, string responseText, Exception? ex = null)
    {
        logger.LogError(ex,
            "Gate {Name}: {Reason}. Response: {Response}",
            role.DisplayName, reason, Truncate(responseText));
        return CommandResult.Fail($"Gate {role.DisplayName}: {reason}");
    }

    private void LogFindings(
        RoleSkillDefinition role, List<Finding> findings, int confirmed, int rejected)
    {
        foreach (var finding in findings)
            logger.LogDebug(
                "[{Gate}] confirmed: {Severity} {File}:{Line} — {Title}",
                role.Name, finding.Severity, finding.File, finding.StartLine, finding.Title);

        logger.LogDebug(
            "[{Gate}] Gate result: {Confirmed} confirmed, {Rejected} rejected",
            role.Name, confirmed, rejected);
    }

    private static string Truncate(string text) =>
        text.Length <= ResponseLogLimit ? text : text[..ResponseLogLimit] + "...[truncated]";
}
