using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses LLM gate output (verdict or finding list) and writes
/// confirmed findings to the pipeline context.
/// </summary>
public sealed class GateOutputHandler(
    ILogger<GateOutputHandler> logger) : IGateOutputHandler
{
    public CommandResult Handle(
        RoleSkillDefinition role,
        SkillOrchestration orchestration,
        string responseText,
        PipelineContext pipeline)
    {
        return orchestration.Output == SkillOutputType.Verdict
            ? HandleVerdict(role, responseText)
            : HandleFindingList(role, responseText, pipeline);
    }

    private static CommandResult HandleVerdict(
        RoleSkillDefinition role, string responseText)
    {
        try
        {
            var json = JsonExtractor.Extract(responseText);
            using var doc = JsonDocument.Parse(json);
            var pass = doc.RootElement.GetProperty("pass").GetBoolean();
            var reason = doc.RootElement.TryGetProperty("reason", out var r)
                ? r.GetString() : "";
            if (!pass)
                return CommandResult.Fail($"Gate veto ({role.DisplayName}): {reason}");
        }
        catch { /* unparseable = pass */ }

        return CommandResult.Ok($"Gate {role.DisplayName}: passed");
    }

    private CommandResult HandleFindingList(
        RoleSkillDefinition role, string responseText, PipelineContext pipeline)
    {
        try
        {
            var json = JsonExtractor.Extract(responseText);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("confirmed", out var confirmed))
                return CommandResult.Ok($"Gate {role.DisplayName}: passed");

            var count = confirmed.GetArrayLength();
            if (count == 0)
            {
                logger.LogInformation("[{Gate}] All findings filtered — no confirmed issues", role.Name);
                return CommandResult.Ok($"Gate {role.DisplayName}: all findings filtered, none confirmed");
            }

            var rejected = doc.RootElement.TryGetProperty("rejected", out var rej)
                ? rej.GetArrayLength() : 0;

            var findings = GateFindingParser.Parse(confirmed);
            pipeline.Set(ContextKeys.ExtractedFindings, findings.AsReadOnly());

            LogFindings(role, findings, count, rejected);

            return CommandResult.Ok(
                $"Gate {role.DisplayName}: {count} findings confirmed");
        }
        catch { /* unparseable = pass */ }

        return CommandResult.Ok($"Gate {role.DisplayName}: passed");
    }

    private void LogFindings(
        RoleSkillDefinition role,
        List<Finding> findings, int confirmed, int rejected)
    {
        foreach (var finding in findings)
            logger.LogDebug(
                "[{Gate}] confirmed: {Severity} {File}:{Line} \u2014 {Title}",
                role.Name, finding.Severity, finding.File, finding.StartLine, finding.Title);

        logger.LogDebug(
            "[{Gate}] Gate result: {Confirmed} confirmed, {Rejected} rejected",
            role.Name, confirmed, rejected);
    }
}
