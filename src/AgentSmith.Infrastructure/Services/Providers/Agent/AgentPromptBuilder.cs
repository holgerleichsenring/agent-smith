using AgentSmith.Infrastructure.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Shared prompt building logic used by all AI agent providers (Claude, OpenAI, Gemini).
/// Provides consistent system and user prompts for plan generation and execution.
/// </summary>
public static class AgentPromptBuilder
{
    public static string BuildPlanSystemPrompt(
        string codingPrinciples, string? codeMap, string? projectContext = null)
    {
        var projectContextSection = BuildProjectContextSection(projectContext);
        var codeMapSection = BuildCodeMapSection(codeMap);

        return $$"""
            You are a senior software engineer. Analyze the following ticket and codebase,
            then create a detailed implementation plan.
            {{projectContextSection}}
            ## Coding Principles
            {{codingPrinciples}}
            {{codeMapSection}}
            ## Respond in JSON format:
            {
              "summary": "Brief summary of what needs to be done",
              "steps": [
                { "order": 1, "description": "...", "target_file": "...", "change_type": "Create|Modify|Delete" }
              ],
              "decisions": [
                { "category": "Architecture|Tooling|Implementation|TradeOff", "decision": "**DecisionName**: reason why, not what" }
              ]
            }

            For every architectural, tooling, or implementation decision in your plan, add an
            entry to the decisions array. Format: "**Decision name**: reason why, not what."
            If the plan is straightforward with no significant decisions, the array may be empty.

            Respond ONLY with the JSON, no additional text.
            """;
    }

    public static string BuildPlanUserPrompt(Ticket ticket, CodeAnalysis codeAnalysis)
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

    public static string BuildExecutionSystemPrompt(
        string codingPrinciples, string? codeMap, string? projectContext = null)
    {
        var projectContextSection = BuildProjectContextSection(projectContext);
        var codeMapSection = BuildCodeMapSection(codeMap);

        return $"""
            {projectContextSection}
            ## Coding Principles
            {codingPrinciples}
            {codeMapSection}
            ## Role
            You are a senior software engineer implementing code changes.
            You have access to tools to read, write, and list files in the repository,
            as well as run shell commands.

            ## Instructions
            - Read existing files before modifying them to understand the current state.
            - Write complete file contents when using write_file (not diffs).
            - Follow the coding principles strictly.
            - Run build and test commands to verify your changes (e.g. dotnet build, dotnet test, npm run build, npm test).
            - NEVER run long-running server processes (dotnet run, npm start, python -m http.server, etc.) — they will time out and block the pipeline.
            - NEVER run interactive commands that require user input.
            - Before each tool call, briefly state what you are doing and why (e.g. "Reading Program.cs to understand the current endpoint structure").
            - When you deviate from the plan or make a non-trivial implementation decision, call the log_decision tool immediately. One sentence. Why, not what. Format: "**Decision name**: reason in one sentence"
            - When done, stop calling tools and summarize what you did.

            ## Human Interaction Rules
            - Ask ONLY when genuinely ambiguous and the wrong choice would cause significant rework.
            - Never ask about implementation details you can decide yourself.
            - Never ask more than once per pipeline stage.
            - Always provide a sensible default_answer so the pipeline can continue on timeout.
            - Prefer logging a decision in log_decision over asking the human.

            Good reasons to ask:
              - Naming that requires domain knowledge (branch name, class name)
              - Ambiguous acceptance criteria in the ticket
              - Destructive operations (delete, rename, breaking change)
              - Multiple equally valid architectural options

            Bad reasons to ask:
              - "Should I add tests?" (always yes)
              - "Which file should I create?" (you decide)
              - "Is this approach okay?" (decide and log in decisions.md)
            """;
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

    public static string BuildExecutionUserPrompt(
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
