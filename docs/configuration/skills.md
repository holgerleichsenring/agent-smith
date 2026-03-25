# Skills Reference

Skills define the AI roles that participate in multi-agent discussions. Each skill is a YAML file in the `config/skills/` directory, organized by category.

## Directory Structure

```
config/skills/
├── coding/              # Used by fix-bug, add-feature pipelines
│   ├── architect.yaml
│   ├── backend-developer.yaml
│   ├── frontend-developer.yaml
│   ├── tester.yaml
│   ├── security-reviewer.yaml
│   ├── devops.yaml
│   ├── dba.yaml
│   └── product-owner.yaml
├── security/            # Used by security-scan pipeline
│   ├── vuln-analyst.yaml
│   ├── auth-reviewer.yaml
│   ├── injection-checker.yaml
│   ├── secrets-detector.yaml
│   └── false-positive-filter.yaml
├── api-security/        # Used by api-scan pipeline
│   ├── api-vuln-analyst.yaml
│   ├── auth-tester.yaml
│   ├── api-design-auditor.yaml
│   └── false-positive-filter.yaml
├── legal/               # Used by legal-analysis pipeline
│   ├── contract-analyst.yaml
│   ├── risk-assessor.yaml
│   ├── liability-analyst.yaml
│   ├── clause-negotiator.yaml
│   └── compliance-checker.yaml
└── mad/                 # Used by mad-discussion pipeline
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

## How Triggers Work

During the triage step, Agent Smith analyzes the ticket/input and extracts signals (e.g., "this ticket touches the database schema and adds a new API endpoint"). It then matches those signals against each role's `triggers` list to decide which roles participate.

A role is activated when **any** of its triggers match. You do not need all triggers to fire.

!!! tip
    Keep triggers broad enough to catch relevant tasks but specific enough to avoid noise. A role that triggers on everything adds cost without value.

## How Convergence Works

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

## Creating Custom Skills

1. Create a new `.yaml` file in the appropriate `config/skills/<category>/` directory
2. Follow the format above
3. Set `skills_path` in your project config to point to the category:

```yaml
projects:
  my-api:
    skills_path: skills/coding    # Uses config/skills/coding/*.yaml
```

All `.yaml` files in the directory are loaded automatically, sorted alphabetically.
