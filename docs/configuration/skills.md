# Skills Reference

Skills define the AI roles that participate in multi-skill pipelines. Each skill has a YAML file in the `config/skills/` directory (organized by category) and an optional `agentsmith.md` file that declares orchestration metadata.

## Directory Structure

```
config/skills/
├── coding/              # Used by fix-bug, add-feature pipelines (hierarchical)
│   ├── architect.yaml
│   ├── backend-developer.yaml
│   ├── frontend-developer.yaml
│   ├── tester.yaml
│   ├── security-reviewer.yaml
│   ├── devops.yaml
│   ├── dba.yaml
│   └── product-owner.yaml
├── security/            # Used by security-scan pipeline (structured)
│   ├── vuln-analyst.yaml
│   ├── auth-reviewer.yaml
│   ├── injection-checker.yaml
│   ├── secrets-detector.yaml
│   └── false-positive-filter.yaml
├── api-security/        # Used by api-scan pipeline (structured)
│   ├── api-vuln-analyst.yaml
│   ├── auth-tester.yaml
│   ├── api-design-auditor.yaml
│   └── false-positive-filter.yaml
├── legal/               # Used by legal-analysis pipeline (discussion)
│   ├── contract-analyst.yaml
│   ├── risk-assessor.yaml
│   ├── liability-analyst.yaml
│   ├── clause-negotiator.yaml
│   └── compliance-checker.yaml
└── mad/                 # Used by mad-discussion pipeline (discussion)
    ├── philosopher.yaml
    ├── dreamer.yaml
    ├── realist.yaml
    ├── devils-advocate.yaml
    └── silencer.yaml
```

## Skill YAML Format

Every skill file follows this structure:

```yaml
name: architect                    # Unique identifier (matches filename)
display_name: "Architect"          # Human-readable name for logs and reports
emoji: "🏗️"                       # Displayed in Slack messages and reports
description: "Evaluates architectural impact, defines component boundaries"

triggers:                          # Signals that activate this role
  - new-component
  - cross-cutting-concern
  - api-design
  - data-model-change
  - pattern-decision
  - integration
  - breaking-change

rules: |                           # System prompt for this role (multi-line)
  You are reviewing this task from an architectural perspective.

  Your responsibilities:
  - Evaluate impact on existing component boundaries
  - Identify cross-cutting concerns (logging, auth, caching)
  - Propose design patterns appropriate for the project's architecture style
  - Flag breaking changes to public APIs or contracts
  - Consider testability and maintainability

  Your constraints:
  - Do NOT propose patterns not already established in the project
  - Do NOT over-engineer — prefer the simplest solution
  - Always reference the project's architecture style from context.yaml

  When disagreeing with another role's proposal:
  - State your concern clearly with reasoning
  - Propose a specific alternative
  - Indicate if this is a blocking concern or a preference

convergence_criteria:              # When this role considers discussion "done"
  - "No unresolved architectural concerns"
  - "Proposed patterns are consistent with project architecture"
  - "Cross-cutting concerns are addressed"
```

## Field Reference

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `name` | Yes | string | Unique identifier, should match filename (without `.yaml`) |
| `display_name` | Yes | string | Human-readable label |
| `emoji` | No | string | Single emoji for display |
| `description` | Yes | string | One-line summary of the role's purpose |
| `triggers` | Yes | list | Keywords that cause the triage step to select this role |
| `rules` | Yes | string | Full system prompt injected when this role speaks |
| `convergence_criteria` | Yes | list | Conditions checked during convergence to end discussion |

## Orchestration Metadata (agentsmith.md)

Each skill can declare an `## orchestration` section in its `agentsmith.md` file. This metadata controls the skill's role, execution order, and output type in structured and hierarchical pipelines.

The orchestration section is **optional**. Skills without it default to `role: contributor` with `output: artifact`, which preserves full backward compatibility with discussion pipelines.

### Format

```markdown
## orchestration
role: contributor
output: list
runs_after: [vuln-analyst, auth-reviewer]
runs_before: [false-positive-filter]
parallel_with: [injection-checker]
input_categories: [authentication, injection]
```

### Orchestration Fields

| Field | Required | Values | Description |
|-------|----------|--------|-------------|
| `role` | No | `contributor`, `lead`, `gate`, `executor` | The skill's role in the pipeline. Defaults to `contributor`. |
| `output` | No | `list`, `plan`, `artifact`, `verdict` | The type of output this skill produces. Defaults to `artifact`. |
| `runs_after` | No | list of role types or skill names | This skill runs after the listed skills/roles complete. |
| `runs_before` | No | list of role types or skill names | This skill runs before the listed skills/roles. |
| `parallel_with` | No | list of skill names | This skill can run concurrently with the listed skills. |
| `input_categories` | No | list of category names | Categories of input this skill processes. |

### Role Descriptions

**contributor** -- Analyzes the problem and produces a JSON list output. Does not block the pipeline. This is the default role.

**lead** -- Runs first in hierarchical pipelines. Produces a plan or directive that is injected into the context of all subsequent skills. Only one lead per pipeline.

**gate** -- Can block the pipeline. With `output: verdict`, emits true/false (false stops the pipeline). With `output: list`, writes typed `List<Finding>` to `ExtractedFindings`; an empty list stops the pipeline.

**executor** -- Acts in the world (creates files, runs commands, applies fixes). Produces an artifact as output.

### Examples by Role

#### Contributor Example (security skill)

```markdown
## orchestration
role: contributor
output: list
runs_after: [lead]
parallel_with: [injection-checker, secrets-detector]
input_categories: [authentication, session-management]
```

This skill runs after the lead, can execute in parallel with other contributors, and produces a list of findings.

#### Lead Example (architect in hierarchical pipeline)

```markdown
## orchestration
role: lead
output: plan
runs_before: [contributor]
```

The lead runs first and produces a plan. All contributors receive this plan in their context.

#### Gate Example (false-positive filter)

```markdown
## orchestration
role: gate
output: list
runs_after: [contributor]
runs_before: [executor]
```

This gate runs after all contributors. It filters findings and writes the result to `ExtractedFindings`. If the filtered list is empty, the pipeline stops (nothing to act on).

#### Executor Example (fix applier)

```markdown
## orchestration
role: executor
output: artifact
runs_after: [gate]
```

The executor runs last, after the gate approves. It produces an artifact (e.g., a code fix, a generated file).

## How Triggers Work

During the triage step, Agent Smith analyzes the ticket/input and extracts signals (e.g., "this ticket touches the database schema and adds a new API endpoint"). It then matches those signals against each role's `triggers` list to decide which roles participate.

A role is activated when **any** of its triggers match. You do not need all triggers to fire.

Triggers are used only in **discussion** pipelines. Structured and hierarchical pipelines include all skills in the category and determine execution order from orchestration metadata.

!!! tip
    Keep triggers broad enough to catch relevant tasks but specific enough to avoid noise. A role that triggers on everything adds cost without value.

## How Convergence Works

Convergence applies only to **discussion** pipelines. Structured and hierarchical pipelines skip the convergence check entirely.

After each discussion round, Agent Smith checks whether all active roles have met their `convergence_criteria`. If all criteria across all roles are satisfied, the discussion ends and the plan is consolidated.

The `discussion` section in `.agentsmith/skill.yaml` controls the safety limits:

```yaml
discussion:
  max_rounds: 3              # Hard cap on discussion rounds
  max_total_commands: 50      # Max commands in consolidated plan
  convergence_threshold: 0    # Min rounds before convergence can trigger
```

## Complete Security Example

```yaml
name: auth-reviewer
display_name: "Auth Reviewer"
emoji: "🔐"
description: "Reviews authentication and authorization patterns"

triggers:
  - security-scan
  - auth
  - login
  - token
  - session
  - rbac
  - permission

rules: |
  You are a security specialist focused on authentication and authorization.

  Review all changed code for:
  - Hardcoded credentials or API keys
  - Missing or weak authentication checks
  - Broken access control (IDOR, privilege escalation)
  - Session management flaws (fixation, missing expiry)
  - JWT issues (missing validation, weak algorithms, no expiry)
  - OAuth/OIDC misconfigurations

  For each finding, provide:
  - severity: HIGH | MEDIUM | LOW
  - file: relative path
  - start_line: integer
  - title: max 80 chars
  - description: detailed explanation with attack vector

convergence_criteria:
  - "All auth-related code paths have been reviewed"
  - "No HIGH severity auth finding left unaddressed"
```

With orchestration metadata in its `agentsmith.md`:

```markdown
## orchestration
role: contributor
output: list
runs_after: [lead]
parallel_with: [injection-checker, secrets-detector]
input_categories: [authentication, session-management]
```

## Creating Custom Skills

1. Create a new `.yaml` file in the appropriate `config/skills/<category>/` directory
2. Follow the format above
3. Optionally create an `agentsmith.md` alongside it with an `## orchestration` section for structured/hierarchical pipelines
4. Set `skills_path` in your project config to point to the category:

```yaml
projects:
  my-api:
    skills_path: skills/coding    # Uses config/skills/coding/*.yaml
```

All `.yaml` files in the directory are loaded automatically, sorted alphabetically.
