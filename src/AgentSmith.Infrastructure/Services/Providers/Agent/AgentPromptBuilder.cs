using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Shared prompt building logic used by all AI agent providers (Claude, OpenAI, Gemini).
/// Provides consistent system and user prompts for plan generation and execution.
/// </summary>
public sealed class AgentPromptBuilder(IPromptCatalog prompts)
{
    public string BuildPlanSystemPrompt(
        string codingPrinciples, string? codeMap, string? projectContext = null)
    {
        return prompts.Render("agent-plan-system", new Dictionary<string, string>
        {
            ["ProjectContextSection"] = BuildProjectContextSection(projectContext),
            ["CodingPrinciples"] = codingPrinciples,
            ["CodeMapSection"] = BuildCodeMapSection(codeMap),
        });
    }

    public string BuildPlanUserPrompt(Ticket ticket, CodeAnalysis codeAnalysis)
    {
        var files = string.Join('\n', codeAnalysis.FileStructure.Take(AgentDefaults.MaxFileStructureLines));
        var deps = string.Join('\n', codeAnalysis.Dependencies);

        return $"""
            ## Ticket
            **ID:** {ticket.Id}
            **Title:** {ticket.Title}
            **Description:** {ticket.Description}
            **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}

            ## Codebase Analysis
            **Language:** {codeAnalysis.Language ?? "Unknown"}
            **Framework:** {codeAnalysis.Framework ?? "Unknown"}

            ### Dependencies
            {deps}

            ### File Structure
            {files}
            """;
    }

    public string BuildExecutionSystemPrompt(
        string codingPrinciples, string? codeMap, string? projectContext = null)
    {
        return prompts.Render("agent-execute-system", new Dictionary<string, string>
        {
            ["ProjectContextSection"] = BuildProjectContextSection(projectContext),
            ["CodingPrinciples"] = codingPrinciples,
            ["CodeMapSection"] = BuildCodeMapSection(codeMap),
        });
    }

    internal static string BuildProjectContextSection(string? projectContext)
    {
        if (string.IsNullOrWhiteSpace(projectContext))
            return "";

        return $"""

            ## Project Context
            This describes the project's identity, architecture, and recent history.
            ```yaml
            {projectContext}
            ```

            """;
    }

    internal static string BuildCodeMapSection(string? codeMap)
    {
        if (string.IsNullOrWhiteSpace(codeMap))
            return "";

        return $"""

            ## Code Map
            Use this map to understand module boundaries, interface contracts, and dependency flow.
            ```yaml
            {codeMap}
            ```

            """;
    }

    public string BuildExecutionUserPrompt(
        Plan plan, Repository repository, ScoutResult? scoutResult = null)
    {
        var steps = string.Join('\n', plan.Steps.Select(
            s => $"  {s.Order}. [{s.ChangeType}] {s.Description} → {s.TargetFile}"));

        var scoutSection = "";
        if (scoutResult is not null && scoutResult.RelevantFiles.Count > 0)
        {
            var files = string.Join('\n', scoutResult.RelevantFiles.Select(f => $"  - {f}"));
            scoutSection = $"""

                ## Scout Results
                The following files have been identified as relevant by the scout agent:
                {files}

                **Scout Summary:** {scoutResult.ContextSummary}

                """;
        }

        var startInstruction = scoutResult is not null
            ? "The scout has already explored the codebase. Proceed directly with implementation."
            : "Start by listing the relevant files, then implement each step.";

        return $"""
            Execute the following implementation plan in repository at: {repository.LocalPath}
            Branch: {repository.CurrentBranch}
            {scoutSection}
            ## Plan
            **Summary:** {plan.Summary}

            **Steps:**
            {steps}

            {startInstruction}
            """;
    }
}
