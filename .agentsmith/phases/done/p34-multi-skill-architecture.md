# Phase 34: Multi-Skill Architecture - Cascading Commands & Role-Based Planning

## Vision

Transform Agent Smith from a single-purpose coding agent into a skill-based orchestration platform.
Skills define **how** Agent Smith approaches a task — what rules apply, what perspective drives
the plan, and what validation checks the output. The pipeline remains a flat command list,
but commands can **insert new commands at runtime**, enabling dynamic multi-skill workflows
like plan discussions between Architect, Developer, and DevOps roles.

This phase introduces:
1. Generalized domain rules (replacing code-specific `coding-principles.md`)
2. Role-based skills with separate rule sets (Architect, Developer, DevOps, Tester, etc.)
3. Cascading command execution (commands insert next commands at runtime)
4. Triage + Plan Discussion with convergence detection
5. Execution Trail in result output

---

## Architecture Decision: Why Cascading, Not Hierarchical

Commands insert follow-up commands **directly after themselves** in the flat list.
No tree structures, no recursive executors, no parallel pipelines.

Reasons:
- At design time, we cannot know which skills are needed or how many rounds of discussion occur
- A flat list is fully transparent: every command is visible in logs, Slack, and the execution trail
- The PipelineExecutor stays simple — iterate a linked list, check for insertions after each step
- Cost tracking, logging, and Slack notifications work unchanged — one message per command

---

## Prerequisite

- Phase 18+ completed (current state)
- Existing `PipelineExecutor`, `CommandExecutor`, `CommandContextFactory`, `PipelineContext` working
- Existing `LoadCodingPrinciplesCommand` handler working

---

## Steps

### Step 1: CommandResult Extension + PipelineExecutor Refactor
See details below in: **Step 1 Details**

Extend `CommandResult` with `InsertNext` property. Refactor `PipelineExecutor` from
index-based iteration to `LinkedList<string>` with runtime insertion support.
Project: `AgentSmith.Contracts/`, `AgentSmith.Application/`

### Step 2: Generalize Domain Rules
See details below in: **Step 2 Details**

Rename `LoadCodingPrinciplesCommand` → `LoadDomainRulesCommand`. Make the command
read its file path from `skill.yaml` context instead of hardcoded `coding_principles_path`.
Update `ContextKeys` accordingly.
Project: `AgentSmith.Contracts/`, `AgentSmith.Application/`

### Step 3: Skill Configuration Schema
See details below in: **Step 3 Details**

Define `SkillConfig`, `RoleConfig`, role YAML schema. Ship default role skill files.
Project: `AgentSmith.Contracts/Configuration/`, `config/skills/`

### Step 4: Init Project Enhancement
See details below in: **Step 4 Details**

Extend the Init Project flow to generate `skill.yaml` alongside `context.yaml`.
Auto-detect which roles are relevant based on project type and stack.
Project: `AgentSmith.Infrastructure/`

### Step 5: SwitchSkillCommand
See details below in: **Step 5 Details**

New command that swaps the active domain rules and skill type in `PipelineContext`.
Project: `AgentSmith.Application/Commands/`

### Step 6: TriageCommand
See details below in: **Step 6 Details**

New command that analyzes the ticket + project context and determines which roles
are needed. Inserts `SkillRoundCommand` entries and a `ConvergenceCheckCommand`
into the pipeline.
Project: `AgentSmith.Application/Commands/`

### Step 7: SkillRoundCommand + ConvergenceCheckCommand
See details below in: **Step 7 Details**

`SkillRoundCommand` loads role-specific rules and generates a planning contribution.
Can insert follow-up commands (e.g., request a response from another role).
`ConvergenceCheckCommand` evaluates whether consensus is reached or more rounds are needed.
Project: `AgentSmith.Application/Commands/`

### Step 8: Execution Trail in Result
See details below in: **Step 8 Details**

Track every executed command with timestamp, skill, and outcome in `PipelineContext`.
Write the trail into `result.md`.
Project: `AgentSmith.Application/`, `AgentSmith.Contracts/`

### Step 9: Slack Integration for Skill Rounds
See details below in: **Step 9 Details**

Each `SkillRoundCommand` posts a Slack message with the role emoji and contribution.
Triage and ConvergenceCheck post summary messages.
Project: `AgentSmith.Infrastructure/`

### Step 10: Tests + Verify
See details below in: **Step 10 Details**

---

## Dependencies

```
Step 1 (CommandResult + PipelineExecutor)
    +-- Step 2 (Generalize Domain Rules)
    |    +-- Step 3 (Skill Configuration Schema)
    |         +-- Step 4 (Init Project Enhancement)
    +-- Step 5 (SwitchSkillCommand) <- needs Step 2
         +-- Step 6 (TriageCommand) <- needs Step 3 + Step 5
              +-- Step 7 (SkillRound + ConvergenceCheck) <- needs Step 6
                   +-- Step 8 (Execution Trail)
                        +-- Step 9 (Slack Integration)
                             +-- Step 10 (Tests + Verify)
```

---

## Step 1 Details: CommandResult Extension + PipelineExecutor Refactor

### CommandResult Change

```
File: src/AgentSmith.Domain/Models/CommandResult.cs
```

Add a single new property to `CommandResult`:

```csharp
public IReadOnlyList<string>? InsertNext { get; init; }
```

Add a new factory method:

```csharp
public static CommandResult OkAndContinueWith(
    string message, params string[] nextCommands)
    => new(true, message)
    {
        InsertNext = nextCommands.Length > 0 ? nextCommands : null
    };
```

All existing `CommandResult.Ok()` and `CommandResult.Fail()` calls remain unchanged.
`InsertNext` defaults to `null` — no behavioral change for existing commands.

### PipelineExecutor Refactor

```
File: src/AgentSmith.Application/Services/PipelineExecutor.cs
```

Replace the current `for` loop with `LinkedList<string>` iteration:

```csharp
public async Task<CommandResult> ExecuteAsync(
    IReadOnlyList<string> commandNames,
    ProjectConfig projectConfig,
    PipelineContext context,
    CancellationToken cancellationToken = default)
{
    var commands = new LinkedList<string>(commandNames);
    var current = commands.First;
    var executionCount = 0;

    while (current is not null)
    {
        if (++executionCount > MaxCommandExecutions)
            return CommandResult.Fail(
                $"Pipeline exceeded maximum of {MaxCommandExecutions} command executions. " +
                "Possible infinite loop in command insertion.");

        var commandName = current.Value;
        logger.LogInformation("Executing {Command}...", commandName);

        var commandContext = contextFactory.Create(
            commandName, projectConfig, context);

        var result = await commandExecutor.ExecuteAsync(
            commandContext, cancellationToken);

        if (!result.IsSuccess)
            return result;

        // Runtime insertion: insert new commands directly after current
        if (result.InsertNext is { Count: > 0 })
        {
            var insertAfter = current;
            foreach (var next in result.InsertNext)
            {
                commands.AddAfter(insertAfter, next);
                insertAfter = insertAfter.Next!;
            }

            logger.LogInformation(
                "{Command} inserted {Count} follow-up commands: {Commands}",
                commandName,
                result.InsertNext.Count,
                string.Join(", ", result.InsertNext));
        }

        current = current.Next;
    }

    return CommandResult.Ok("Pipeline completed");
}
```

### Safety: Max Command Limit

Add a hard limit to prevent infinite loops:

```csharp
private const int MaxCommandExecutions = 100;
```

### Tests

- `ExecuteAsync_CommandInsertsFollowUp_ExecutesInsertedCommands`
- `ExecuteAsync_CommandInsertsMultiple_ExecutesInCorrectOrder`
- `ExecuteAsync_NoInsertions_WorksAsBeforeRegression`
- `ExecuteAsync_ExceedsMaxCommands_ReturnsFail`
- `ExecuteAsync_InsertedCommandFails_StopsImmediately`

---

## Step 2 Details: Generalize Domain Rules

### Rename Command

Rename `LoadCodingPrinciplesCommand` → `LoadDomainRulesCommand` across all files:
- `LoadCodingPrinciplesContext` → `LoadDomainRulesContext`
- `LoadCodingPrinciplesHandler` → `LoadDomainRulesHandler`
- Context file, handler file, test files

### ContextKeys Update

```
File: src/AgentSmith.Contracts/Commands/ContextKeys.cs
```

```csharp
// Keep old key as alias for backward compatibility
public const string CodingPrinciples = "DomainRules";  // Alias
public const string DomainRules = "DomainRules";
public const string ActiveSkill = "ActiveSkill";        // NEW
public const string ExecutionTrail = "ExecutionTrail";   // NEW
public const string DiscussionLog = "DiscussionLog";     // NEW
public const string ConsolidatedPlan = "ConsolidatedPlan"; // NEW
```

### Handler Change

The handler reads from `context.FilePath` as before. No logic change needed.
The file path now comes from `skill.yaml -> context.rules` instead of
`project.coding_principles_path` (resolved in Step 3).

### Config Backward Compatibility

If `skill.yaml` does not exist but `coding_principles_path` is set in `agentsmith.yml`,
fall back to the old behavior. This ensures all existing setups keep working.

### CommandContextFactory Update

Update the switch expression:
- `"LoadCodingPrinciples"` still works (maps to same handler)
- `"LoadDomainRules"` is the new canonical name
- Both produce `LoadDomainRulesContext`

---

## Step 3 Details: Skill Configuration Schema

### Role Skill File Format

```
Directory: config/skills/
```

Each role is a YAML file with a fixed structure:

```yaml
# config/skills/architect.yaml
name: architect
display_name: "Architect"
emoji: "🏗️"
description: "Evaluates architectural impact, defines component boundaries and patterns"

# What triggers this role in triage
triggers:
  - new-component
  - cross-cutting-concern
  - api-design
  - data-model-change
  - pattern-decision
  - integration
  - breaking-change

# The system prompt addition when this role is active
rules: |
  You are reviewing this task from an architectural perspective.

  Your responsibilities:
  - Evaluate impact on existing component boundaries
  - Identify cross-cutting concerns (logging, auth, caching)
  - Propose design patterns appropriate for the project's architecture style
  - Flag breaking changes to public APIs or contracts
  - Consider testability and maintainability of proposed approach
  - Ensure consistency with the project's established patterns (see context.yaml)

  Your constraints:
  - Do NOT propose patterns not already established in the project unless justified
  - Do NOT over-engineer — prefer the simplest solution that satisfies requirements
  - Always reference the project's architecture style from context.yaml

  When disagreeing with another role's proposal:
  - State your concern clearly with reasoning
  - Propose a specific alternative
  - Indicate if this is a blocking concern or a preference

# What this role checks during convergence
convergence_criteria:
  - "No unresolved architectural concerns"
  - "Proposed patterns are consistent with project architecture"
  - "Cross-cutting concerns are addressed"
```

### Default Role Files to Ship (Open Source)

```
config/skills/
+-- architect.yaml
+-- backend-developer.yaml
+-- frontend-developer.yaml
+-- devops.yaml
+-- tester.yaml
+-- dba.yaml
+-- product-owner.yaml
+-- security-reviewer.yaml
```

Each follows the same schema. The `rules` section is the key differentiator.

Example `backend-developer.yaml`:

```yaml
name: backend-developer
display_name: "Backend Developer"
emoji: "👨‍💻"
description: "Evaluates implementation feasibility, estimates effort, proposes code structure"

triggers:
  - implementation
  - bug-fix
  - refactoring
  - performance
  - api-endpoint
  - business-logic

rules: |
  You are reviewing this task from a backend implementation perspective.

  Your responsibilities:
  - Assess implementation feasibility and effort
  - Propose concrete code structure (classes, methods, interfaces)
  - Identify reusable existing code and patterns in the codebase
  - Flag potential performance concerns
  - Consider error handling and edge cases
  - Ensure the domain rules (coding principles) are followed

  Your constraints:
  - Work within the existing project structure — do not reorganize
  - Prefer modifying existing classes over creating new ones when appropriate
  - Follow the domain rules file strictly

  When disagreeing with another role's proposal:
  - Explain the implementation cost or risk
  - Propose a simpler alternative if applicable
  - Indicate if you can work around it or if it blocks implementation

convergence_criteria:
  - "Implementation approach is clear and feasible"
  - "No open questions about code structure"
```

Example `devops.yaml`:

```yaml
name: devops
display_name: "DevOps"
emoji: "⚙️"
description: "Evaluates infrastructure impact, CI/CD changes, deployment concerns"

triggers:
  - infrastructure
  - ci-cd
  - deployment
  - configuration
  - environment
  - docker
  - kubernetes
  - monitoring

rules: |
  You are reviewing this task from an infrastructure and deployment perspective.

  Your responsibilities:
  - Assess impact on CI/CD pipelines
  - Identify new infrastructure requirements (services, databases, queues)
  - Flag deployment risks or required configuration changes
  - Check if existing infrastructure supports the proposed changes
  - Consider monitoring and observability needs

  Your constraints:
  - Do NOT propose new infrastructure unless strictly necessary
  - Prefer using existing tools and services already in the stack
  - Always consider the cost of infrastructure changes

  When disagreeing with another role's proposal:
  - Explain the infrastructure cost or operational risk
  - Propose a simpler deployment approach
  - Indicate timeline impact

convergence_criteria:
  - "No new infrastructure required, or new infrastructure is justified"
  - "CI/CD impact is understood and manageable"
  - "No deployment risks unaddressed"
```

### Project-Level Skill Configuration

```
File: .agentsmith/skill.yaml
```

```yaml
# Skill configuration for this project.
# Generated by 'agentsmith init', can be customized.

# Which input/output types this project uses
input:
  type: ticket                    # ticket | document | request
  provider: github                # github | jira | azure-devops | filesystem

output:
  type: pull-request              # pull-request | report | artifact
  provider: github                # github | filesystem | slack

# Domain context files (relative to .agentsmith/)
context:
  rules: coding-principles.md     # Domain rules file
  map: code-map.yaml              # Domain map file

# Roles available for this project
# Triage selects from enabled roles at runtime per ticket
roles:
  architect:
    enabled: true
    extra_rules: |
      This project uses CQRS with MediatR-style command/handler pattern.
      New features must maintain command/query separation.

  backend-developer:
    enabled: true

  frontend-developer:
    enabled: false                 # Pure backend project

  devops:
    enabled: true
    extra_rules: |
      Deployment uses Docker Compose locally and Kubernetes in production.
      ArgoCD handles production deployments — no manual kubectl changes.

  tester:
    enabled: true

  dba:
    enabled: false                 # No direct database in this project

  product-owner:
    enabled: false                 # Tickets are pre-refined

  security-reviewer:
    enabled: false

# Discussion settings
discussion:
  max_rounds: 3                   # Max full rounds (each role gets max 3 turns)
  max_total_commands: 50          # Hard limit on total commands in discussion
  convergence_threshold: 0        # 0 = all must agree, 1 = one dissent allowed
```

### Config Models

```
File: src/AgentSmith.Contracts/Models/Configuration/SkillConfig.cs
```

```csharp
public sealed record SkillConfig
{
    public required InputConfig Input { get; init; }
    public required OutputConfig Output { get; init; }
    public required ContextConfig Context { get; init; }
    public required Dictionary<string, RoleProjectConfig> Roles { get; init; }
    public required DiscussionConfig Discussion { get; init; }
}

public sealed record InputConfig
{
    public required string Type { get; init; }
    public required string Provider { get; init; }
}

public sealed record OutputConfig
{
    public required string Type { get; init; }
    public required string Provider { get; init; }
}

public sealed record ContextConfig
{
    public required string Rules { get; init; }
    public required string Map { get; init; }
}

public sealed record RoleProjectConfig
{
    public bool Enabled { get; init; } = true;
    public string? ExtraRules { get; init; }
}

public sealed record DiscussionConfig
{
    public int MaxRounds { get; init; } = 3;
    public int MaxTotalCommands { get; init; } = 50;
    public int ConvergenceThreshold { get; init; } = 0;
}
```

```
File: src/AgentSmith.Contracts/Models/Configuration/RoleSkillDefinition.cs
```

```csharp
public sealed record RoleSkillDefinition
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Emoji { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> Triggers { get; init; }
    public required string Rules { get; init; }
    public required IReadOnlyList<string> ConvergenceCriteria { get; init; }
}
```

### ISkillLoader Interface

```
File: src/AgentSmith.Contracts/Services/ISkillLoader.cs
```

```csharp
public interface ISkillLoader
{
    /// <summary>
    /// Loads the project-level skill.yaml from the .agentsmith/ directory.
    /// Returns null if no skill.yaml exists (backward compatibility).
    /// </summary>
    SkillConfig? LoadProjectSkills(string agentSmithDirectory);

    /// <summary>
    /// Loads all role skill definitions from the config/skills/ directory.
    /// </summary>
    IReadOnlyList<RoleSkillDefinition> LoadRoleDefinitions(string skillsDirectory);

    /// <summary>
    /// Merges role definitions with project-level overrides (extra_rules, enabled).
    /// Returns only enabled roles.
    /// </summary>
    IReadOnlyList<RoleSkillDefinition> GetActiveRoles(
        IReadOnlyList<RoleSkillDefinition> allRoles,
        SkillConfig projectSkills);
}
```

Implementation: `YamlSkillLoader` in `AgentSmith.Infrastructure/Services/`.

---

## Step 4 Details: Init Project Enhancement

### Current Init Behavior

The Init Project step currently generates:
- `.agentsmith/context.yaml` — project metadata, stack, architecture
- `.agentsmith/coding-principles.md` — detected coding rules
- `.agentsmith/code-map.yaml` — project structure map

### New: Generate `skill.yaml`

After generating `context.yaml`, the Init step analyzes the project to determine
which roles should be enabled. The detection logic is based on `context.yaml` content:

```
If stack contains frontend framework (React, Angular, Vue, Blazor):
  -> enable frontend-developer

If stack contains backend framework (.NET, Spring, Django, Express):
  -> enable backend-developer

If infra contains Docker, Kubernetes, Terraform, CI/CD:
  -> enable devops

If infra contains database (SQL Server, PostgreSQL, MongoDB):
  -> enable dba

If type contains [web-api, rest-api, grpc]:
  -> enable architect (API design decisions)

If testing frameworks detected:
  -> enable tester

Always enable:
  -> architect (every project benefits from architecture review)
  -> backend-developer OR frontend-developer (at least one)
```

The generated `skill.yaml` includes comments explaining why each role was enabled/disabled:

```yaml
# Auto-generated by 'agentsmith init' based on project analysis.
# Customize as needed.

input:
  type: ticket
  provider: github              # Detected from source config

output:
  type: pull-request
  provider: github

context:
  rules: coding-principles.md
  map: code-map.yaml

roles:
  architect:
    enabled: true               # Always enabled for architecture review
  backend-developer:
    enabled: true               # .NET 8 backend detected
  frontend-developer:
    enabled: false              # No frontend framework detected
  devops:
    enabled: true               # Docker detected in project
  tester:
    enabled: true               # xUnit testing framework detected
  dba:
    enabled: false              # No database detected
  product-owner:
    enabled: false              # Enable if tickets need scope refinement
  security-reviewer:
    enabled: false              # Enable for security-sensitive projects

discussion:
  max_rounds: 3
  max_total_commands: 50
  convergence_threshold: 0
```

### Backward Compatibility

If `.agentsmith/skill.yaml` does not exist, Agent Smith falls back to single-skill
mode with the existing `coding_principles_path` from `agentsmith.yml`. The system
works exactly as before — no skill.yaml = no triage, no discussion, no role switching.

---

## Step 5 Details: SwitchSkillCommand

### Context

```
File: src/AgentSmith.Application/Models/SwitchSkillContext.cs
```

```csharp
public sealed record SwitchSkillContext(
    string SkillName,
    PipelineContext Pipeline) : ICommandContext;
```

### Handler

```
File: src/AgentSmith.Application/Services/Handlers/SwitchSkillHandler.cs
```

```csharp
public sealed class SwitchSkillHandler(
    ILogger<SwitchSkillHandler> logger)
    : ICommandHandler<SwitchSkillContext>
{
    public async Task<CommandResult> ExecuteAsync(
        SwitchSkillContext context, CancellationToken ct)
    {
        var roles = context.Pipeline.Get<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles);

        var role = roles.FirstOrDefault(r => r.Name == context.SkillName)
            ?? throw new ConfigurationException(
                $"Role '{context.SkillName}' not found in available roles.");

        // Load merged rules: base role rules + project-level extra_rules
        var projectSkills = context.Pipeline.TryGet<SkillConfig>(
            ContextKeys.ProjectSkills, out var skills) ? skills : null;

        var extraRules = projectSkills?.Roles
            .GetValueOrDefault(context.SkillName)?.ExtraRules;

        var mergedRules = extraRules is not null
            ? $"{role.Rules}\n\n## Project-Specific Rules\n{extraRules}"
            : role.Rules;

        // Swap active rules in PipelineContext
        context.Pipeline.Set(ContextKeys.DomainRules, mergedRules);
        context.Pipeline.Set(ContextKeys.ActiveSkill, context.SkillName);

        logger.LogInformation(
            "Switched to skill: {Emoji} {DisplayName}",
            role.Emoji, role.DisplayName);

        return CommandResult.Ok(
            $"Switched to {role.DisplayName} perspective");
    }
}
```

### CommandContextFactory Update

```csharp
// New entries in the switch expression:
"SwitchSkillCommand" => new SwitchSkillContext(
    ExtractSkillName(commandName),  // "SwitchSkillCommand:architect" -> "architect"
    pipeline),
```

### Command Name Convention

Parameterized commands use `:` separator:
- `SwitchSkillCommand:architect`
- `SkillRoundCommand:architect:1`
- `ConvergenceCheckCommand`

The `CommandContextFactory` parses the segments to extract parameters.

---

## Step 6 Details: TriageCommand

### Context

```
File: src/AgentSmith.Application/Models/TriageContext.cs
```

```csharp
public sealed record TriageContext(
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
```

### Handler

```
File: src/AgentSmith.Application/Services/Handlers/TriageHandler.cs
```

The Triage handler:
1. Reads the ticket, context analysis, and available roles from PipelineContext
2. Makes a single LLM call to determine which roles are needed and who leads
3. Inserts `SkillRoundCommand` entries + `ConvergenceCheckCommand` into the pipeline

**LLM Prompt Template:**

```
You are triaging a development ticket to determine which specialist roles
should participate in planning.

## Ticket
{ticket.Title}
{ticket.Description}

## Project Context
{context.yaml summary}

## Available Roles
{for each enabled role: name, description, triggers}

## Instructions
Analyze the ticket and determine:
1. Which roles are needed (select from available roles only)
2. Who should lead the discussion (creates the initial plan)
3. Brief justification for each selected role

Respond in JSON:
{
  "lead": "architect",
  "participants": ["architect", "backend-developer", "devops"],
  "justification": {
    "architect": "New component boundary needed",
    "backend-developer": "Implementation of new handler",
    "devops": "Redis Streams integration"
  },
  "complexity": "high",
  "estimated_rounds": 2
}
```

**InsertNext Logic:**

```csharp
var commandsToInsert = new List<string>();

// Lead goes first
commandsToInsert.Add($"SkillRoundCommand:{result.Lead}:1");

// Other participants follow
foreach (var participant in result.Participants.Where(p => p != result.Lead))
{
    commandsToInsert.Add($"SkillRoundCommand:{participant}:1");
}

// Convergence check at the end
commandsToInsert.Add("ConvergenceCheckCommand");

return CommandResult.OkAndContinueWith(
    $"Triage complete. Lead: {result.Lead}. " +
    $"Participants: {string.Join(", ", result.Participants)}",
    commandsToInsert.ToArray());
```

**Simple Tickets:**

If the triage determines only one role is needed (e.g., simple bug fix -> backend-developer only),
it skips the discussion entirely and returns `Ok` without inserting any commands.
The pipeline continues with `ApprovalCommand` -> `AgenticExecuteCommand` as before.

---

## Step 7 Details: SkillRoundCommand + ConvergenceCheckCommand

### SkillRoundCommand

#### Context

```csharp
public sealed record SkillRoundContext(
    string SkillName,
    int Round,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
```

#### Handler Logic

1. Load role rules (base + extra_rules from project)
2. Build prompt with: ticket, project context, role rules, **all previous discussion entries**
3. Make LLM call
4. Append response to `ContextKeys.DiscussionLog` in PipelineContext
5. Check if response contains objections or questions for another role
6. If yes -> insert follow-up commands

**Prompt Template:**

```
## Your Role
{role.DisplayName}: {role.Description}

## Role-Specific Rules
{role.Rules}
{extraRules if present}

## Ticket
{ticket}

## Project Context
{context.yaml}
{domain-map.yaml}
{domain-rules.md}

## Discussion So Far
{all previous SkillRound entries, formatted as:}
---
Architect (Round 1):
[architect's contribution]

DevOps (Round 1):
[devops contribution]
---

## Your Task
Based on the discussion so far, provide your perspective on this ticket.

If this is the first round for the lead role: Create an initial implementation plan.
If responding to an existing plan: Review it from your perspective.

At the end of your response, state clearly:
- AGREE: if you accept the current plan
- OBJECTION [target_role]: if you have a blocking concern for a specific role
- SUGGESTION: if you have a non-blocking improvement

Keep your contribution focused and concise (max 500 words).
```

**InsertNext Logic:**

```csharp
if (response contains "OBJECTION" targeting a role)
{
    var targetRole = ExtractTargetRole(response);
    var nextRound = round + 1;

    return CommandResult.OkAndContinueWith(
        $"{role.DisplayName} objects, requesting response from {targetRole}",
        $"SkillRoundCommand:{targetRole}:{nextRound}",
        $"SkillRoundCommand:{skillName}:{nextRound}",  // Follow up on response
        "ConvergenceCheckCommand");
}

// AGREE or SUGGESTION -> no insertion, let pipeline continue to ConvergenceCheck
return CommandResult.Ok($"{role.DisplayName}: {verdict}");
```

### ConvergenceCheckCommand

#### Context

```csharp
public sealed record ConvergenceCheckContext(
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
```

#### Handler Logic

1. Read all discussion log entries from PipelineContext
2. Check: Are there any unresolved OBJECTION entries without a follow-up AGREE?
3. If all resolved -> consolidate plan and store in `ContextKeys.ConsolidatedPlan`
4. If unresolved AND under max rounds -> insert more `SkillRoundCommand` entries
5. If unresolved AND at max rounds -> consolidate with dissent noted, escalate to human

**Convergence Detection Options:**

Option A (Simple — recommended for first implementation):
- Parse discussion log for last entry per role
- If all end with AGREE or SUGGESTION -> converged
- If any end with OBJECTION -> not converged

Option B (LLM-based — future enhancement):
- Send full discussion to LLM with convergence criteria
- LLM evaluates whether meaningful progress is being made
- Can detect circular arguments

**Consolidation:**

When converged, the handler makes one final LLM call to consolidate:

```
You are consolidating a planning discussion into a final implementation plan.

## Discussion
{full discussion log}

## Task
Create a consolidated plan that incorporates all agreed-upon decisions.
Structure it as:

{
  "summary": "...",
  "steps": [
    {
      "order": 1,
      "skill": "backend-developer",
      "description": "...",
      "target_files": ["..."],
      "depends_on": []
    }
  ],
  "execution_order": ["devops", "backend-developer", "backend-developer"],
  "key_decisions": [
    { "topic": "...", "decision": "...", "proposed_by": "architect" }
  ]
}
```

The consolidated plan replaces the standard `Plan` in `PipelineContext`.
`GeneratePlanCommand` is skipped when a consolidated plan already exists
(the plan came from the discussion, not from a single-skill call).

---

## Step 8 Details: Execution Trail in Result

### ExecutionTrail Record

```
File: src/AgentSmith.Contracts/Commands/ExecutionTrailEntry.cs
```

```csharp
public sealed record ExecutionTrailEntry(
    string CommandName,
    string? Skill,
    bool Success,
    string Message,
    DateTimeOffset Timestamp,
    TimeSpan Duration,
    int? InsertedCommandCount);
```

### PipelineContext Extension

Add helper method to `PipelineContext`:

```csharp
public void TrackCommand(string commandName, CommandResult result, TimeSpan duration)
{
    var trail = TryGet<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail, out var existing)
        ? existing
        : new List<ExecutionTrailEntry>();

    trail.Add(new ExecutionTrailEntry(
        commandName,
        TryGet<string>(ContextKeys.ActiveSkill, out var skill) ? skill : null,
        result.IsSuccess,
        result.Message,
        DateTimeOffset.UtcNow,
        duration,
        result.InsertNext?.Count));

    Set(ContextKeys.ExecutionTrail, trail);
}
```

### Result.md Format

The existing `result.md` output gets an `## Execution Trail` section:

```markdown
## Execution Trail

| # | Command | Skill | Result | Duration | Inserted |
|---|---------|-------|--------|----------|----------|
| 1 | FetchTicket | - | OK: Ticket fetched | 1.2s | - |
| 2 | CheckoutSource | - | OK: Branch created | 3.4s | - |
| 3 | LoadDomainRules | - | OK: Rules loaded | 0.1s | - |
| 4 | AnalyzeCode | - | OK: Context analyzed | 2.1s | - |
| 5 | Triage | - | OK: Lead: architect | 4.2s | +4 |
| 6 | SkillRound:architect:1 | architect | OK: Plan created | 8.3s | - |
| 7 | SkillRound:devops:1 | devops | OK: No concerns | 5.1s | - |
| 8 | SkillRound:backend-dev:1 | backend-developer | OK: Objection | 6.7s | +3 |
| 9 | SkillRound:architect:2 | architect | OK: Adjusted | 5.4s | - |
| 10 | SkillRound:backend-dev:2 | backend-developer | OK: Agreed | 3.2s | - |
| 11 | ConvergenceCheck | - | OK: Consensus | 4.8s | - |
| 12 | Approval | - | OK: Approved | 0.0s | - |
| 13 | AgenticExecute | backend-developer | OK: 3 files changed | 22.1s | - |
| 14 | Test | - | OK: 12 tests passed | 8.9s | - |
| 15 | CommitAndPR | - | OK: PR #42 created | 3.1s | - |

**Total: 15 commands, 87.6s, $0.34**
```

---

## Step 9 Details: Slack Integration for Skill Rounds

### Message Format per Command Type

**TriageCommand:**
```
*Triage Complete*
Complexity: High
Lead: Architect
Participants: Architect, Backend Developer, DevOps
```

**SkillRoundCommand:**
```
*Architect (Round 1):*
[contribution text, truncated to 500 chars for Slack]
Verdict: AGREE
```

```
*Backend Developer (Round 1):*
[contribution text]
Verdict: OBJECTION -> Architect
```

**ConvergenceCheckCommand:**
```
*Consensus reached after 5 rounds*
Key decisions:
- MediatR Notification pattern (proposed by Architect)
- Reuse existing Redis connection (confirmed by DevOps)
```

**OR if escalated:**
```
*No consensus after 3 rounds*
Open dissent: Architect vs Backend Developer on event pattern
Awaiting human decision in approval step.
```

### Implementation

The Slack posting logic already exists in the pipeline for ticket status updates.
Each new handler calls the same `INotificationService` (or the existing Slack integration)
to post its message. No new infrastructure needed.

---

## Step 10 Details: Tests + Verify

### Unit Tests

**CommandResult:**
- `OkAndContinueWith_WithCommands_SetsInsertNext`
- `Ok_Standard_InsertNextIsNull`

**PipelineExecutor:**
- `ExecuteAsync_CommandInsertsFollowUp_ExecutesInsertedCommands`
- `ExecuteAsync_MultipleInsertions_MaintainsCorrectOrder`
- `ExecuteAsync_InsertedCommandAlsoInserts_CascadesCorrectly`
- `ExecuteAsync_ExceedsMaxCommands_ReturnsFail`
- `ExecuteAsync_NoSkillYaml_FallsBackToSingleSkill`

**SkillLoader:**
- `LoadRoleDefinitions_ValidYaml_ReturnsAllRoles`
- `LoadProjectSkills_ValidSkillYaml_ReturnsMergedConfig`
- `GetActiveRoles_DisabledRoles_ExcludesFromResult`
- `LoadProjectSkills_NoSkillYaml_ReturnsNull`

**SwitchSkillHandler:**
- `ExecuteAsync_ValidRole_SwitchesDomainRules`
- `ExecuteAsync_WithExtraRules_MergesRules`
- `ExecuteAsync_UnknownRole_ThrowsConfigurationException`

**TriageHandler:**
- `ExecuteAsync_ComplexTicket_InsertsMultipleRounds`
- `ExecuteAsync_SimpleTicket_NoInsertion`

**SkillRoundHandler:**
- `ExecuteAsync_WithObjection_InsertsFollowUp`
- `ExecuteAsync_WithAgree_NoInsertion`
- `ExecuteAsync_AppendsToDiscussionLog`

**ConvergenceCheckHandler:**
- `ExecuteAsync_AllAgreed_StoresConsolidatedPlan`
- `ExecuteAsync_OpenObjections_InsertsMoreRounds`
- `ExecuteAsync_MaxRoundsReached_EscalatesToHuman`
- `ExecuteAsync_AlreadyConverged_NoOp`

**ExecutionTrail:**
- `TrackCommand_AddsEntry`
- `TrackCommand_MultipleCalls_MaintainsOrder`

### Integration Test

- Full pipeline with mocked LLM that returns scripted triage + discussion responses
- Verifies: correct command insertion order, convergence detection, execution trail output

### Verify

```bash
dotnet build
dotnet test
```

---

## Pipeline Configuration

### New Pipeline Example with Discussion

```yaml
pipelines:
  feature-with-discussion:
    commands:
      - FetchTicket
      - CheckoutSource
      - BootstrapProject
      - LoadCodeMap
      - LoadDomainRules
      - LoadContext
      - AnalyzeCode
      - Triage                     # Determines roles, inserts discussion commands
      # SkillRoundCommands are inserted here at runtime by TriageCommand
      - Approval                   # Human reviews the consolidated plan
      - AgenticExecute
      - Test
      - WriteRunResult
      - CommitAndPR

  fix-bug:                         # Simple pipeline, no discussion
    commands:
      - FetchTicket
      - CheckoutSource
      - BootstrapProject
      - LoadCodeMap
      - LoadDomainRules
      - LoadContext
      - AnalyzeCode
      - GeneratePlan
      - Approval
      - AgenticExecute
      - Test
      - WriteRunResult
      - CommitAndPR
```

The `fix-bug` pipeline works exactly as before — no `Triage`, no discussion.
The `feature-with-discussion` pipeline adds triage and lets the system decide dynamically
how many rounds of discussion are needed.

---

## NuGet Packages

No new NuGet packages required. All functionality uses existing dependencies:
- YamlDotNet for skill YAML parsing
- Existing LLM providers for triage and discussion calls
- Existing Slack integration for notifications

---

## Key Decisions Summary

1. **Flat list, not tree** — Commands insert into a LinkedList, no hierarchical execution
2. **Backward compatible** — No `skill.yaml` = old behavior, no breaking changes
3. **Role rules are shipped** — Open source default roles, closed source custom roles
4. **Init auto-detects roles** — Based on `context.yaml` analysis, human can override
5. **Simple convergence first** — Parse AGREE/OBJECTION markers, LLM-based convergence later
6. **Max safety limits** — Max 100 commands total, max 3 rounds per role, max 50 discussion commands
7. **Parameterized command names** — `SkillRoundCommand:architect:1` parsed by CommandContextFactory
8. **SwitchSkill is explicit** — No hidden context changes, every skill switch is a visible command

---

## Definition of Done

- [ ] `CommandResult.InsertNext` property + `OkAndContinueWith` factory method
- [ ] `PipelineExecutor` uses `LinkedList<string>` with runtime insertion
- [ ] Max command execution limit (100) prevents infinite loops
- [ ] `LoadDomainRulesCommand` replaces `LoadCodingPrinciplesCommand` (backward compatible)
- [ ] New `ContextKeys`: `ActiveSkill`, `ExecutionTrail`, `DiscussionLog`, `ConsolidatedPlan`
- [ ] `SkillConfig`, `RoleSkillDefinition`, `RoleProjectConfig` config models
- [ ] `ISkillLoader` interface + `YamlSkillLoader` implementation
- [ ] Default role YAML files shipped: architect, backend-developer, devops, tester (minimum)
- [ ] Init Project generates `skill.yaml` with auto-detected role recommendations
- [ ] `SwitchSkillCommand` swaps active domain rules in PipelineContext
- [ ] `TriageCommand` analyzes ticket and inserts `SkillRoundCommand` entries
- [ ] `SkillRoundCommand` generates role-specific planning contribution
- [ ] `SkillRoundCommand` inserts follow-up commands on OBJECTION
- [ ] `ConvergenceCheckCommand` detects consensus or escalates
- [ ] Consolidated plan stored in PipelineContext when discussion completes
- [ ] `ExecutionTrailEntry` records every command with timestamp, skill, duration
- [ ] Execution trail written to `result.md`
- [ ] Slack messages posted per SkillRound with role emoji and contribution
- [ ] All existing tests green (no regressions)
- [ ] New unit tests for all new commands and services
- [ ] Integration test with scripted multi-round discussion
- [ ] `dotnet build` + `dotnet test` pass clean
