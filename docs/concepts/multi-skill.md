# Multi-Skill Architecture

For complex tasks, Agent Smith assembles a panel of AI specialists that debate the approach before execution.

## How It Works

1. **Triage** — looks at the ticket and codebase, selects relevant roles
2. **Skill Rounds** — each role analyzes the problem and states its position
3. **Convergence** — roles discuss until they agree on an approach
4. **Execution** — the consolidated plan goes to the agentic loop

## Roles

Roles are defined in YAML files under `config/skills/`. Each role has:

- **triggers** — what activates it (e.g. `database`, `authentication`, `api-design`)
- **rules** — system prompt defining the role's perspective and responsibilities
- **convergence_criteria** — when the role is satisfied

### Example: Architect Role

```yaml
name: architect
display_name: "Architect"
emoji: "🏗️"
description: "Evaluates architectural impact, defines component boundaries"

triggers:
  - new-component
  - cross-cutting-concern
  - api-design
  - breaking-change

rules: |
  You are reviewing this task from an architectural perspective.
  Your responsibilities:
  - Evaluate impact on existing component boundaries
  - Identify cross-cutting concerns
  - Flag breaking changes to public APIs
  - Ensure consistency with established patterns

convergence_criteria:
  - "No unresolved architectural concerns"
  - "Proposed patterns are consistent with project architecture"
```

## Skill Categories

Agent Smith ships with role sets for different domains:

| Directory | Roles | Used by |
|-----------|-------|---------|
| `config/skills/coding/` | Architect, Backend Dev, Tester, DBA, Security, DevOps, Frontend, Product Owner | fix-bug, add-feature |
| `config/skills/security/` | Vulnerability Analyst, Auth Reviewer, Injection Checker, Secrets Detector, False Positive Filter | security-scan |
| `config/skills/api-security/` | API Design Auditor, Auth Tester, Vulnerability Analyst, False Positive Filter | api-scan |
| `config/skills/legal/` | Contract Analyst, Compliance Checker, Risk Assessor, Liability Analyst, Clause Negotiator | legal-analysis |
| `config/skills/mad/` | Philosopher, Dreamer, Realist, Devil's Advocate, Silencer | mad-discussion |

## Discussion Flow

Each discussion runs in rounds:

```
Round 1:
  Architect:  "OBJECT — this violates our layering rules. Propose: add interface in Contracts."
  DBA:        "AGREE — schema change is backward compatible."
  Tester:     "SUGGEST — add integration test for the new endpoint."

Round 2:
  Architect:  "AGREE — interface added to the plan."
  Tester:     "AGREE — integration test included."

→ Consensus reached after 2 rounds.
→ Consolidated plan goes to execution.
```

Each role can:

- **AGREE** — satisfied with the current plan
- **OBJECT** — has a blocking concern (must include reasoning and alternative)
- **SUGGEST** — has a non-blocking improvement

The discussion ends when all active roles agree. A hard limit prevents endless debates (configurable, default 3 rounds).

## Project-Specific Skills

Projects can override or extend the default roles. Put a `skills.yaml` in your project's `.agentsmith/` directory to select which roles are active:

```yaml
roles:
  - architect
  - backend-developer
  - tester
```

This activates only those three roles for the project, ignoring DBA, DevOps, etc.

## Custom Roles

Create your own role by adding a YAML file to your skills directory:

```yaml
name: performance-engineer
display_name: "Performance Engineer"
emoji: "⚡"
description: "Evaluates performance impact of proposed changes"

triggers:
  - database
  - api-endpoint
  - caching

rules: |
  You review changes for performance implications.
  Flag N+1 queries, missing indexes, unbounded collections,
  and unnecessary allocations.

convergence_criteria:
  - "No performance concerns above medium severity"
```
