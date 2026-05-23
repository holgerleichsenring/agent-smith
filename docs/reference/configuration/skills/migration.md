# SKILL.md Migration

This page shows the before/after for migrating a skill from the legacy `agentsmith.md` `## orchestration` model to the p0111 frontmatter-driven model.

## Before — legacy

Two files per skill: `SKILL.md` (frontmatter + body) and `agentsmith.md` (orchestration metadata).

`SKILL.md`:

```markdown
---
name: architect
display_name: Architect
emoji: 🏛️
description: System architect who plans changes.
triggers: [feature, refactor]
---

You are the architect. Your role is to plan...
```

`agentsmith.md`:

```markdown
## orchestration

role: lead
output: artifact
runs_before: contributor
input_categories: *
```

The orchestration metadata drove a deterministic graph builder (`SkillGraphBuilder`) that decided execution order and roles per skill globally — no per-ticket triage.

## After — p0111

Single `SKILL.md` with extended frontmatter. `agentsmith.md` is gone for orchestration purposes; `SKILL.md` is the single source of truth.

```markdown
---
name: architect
display_name: Architect
emoji: 🏛️
description: System architect who plans architectural changes.
roles_supported: [lead, analyst, reviewer]

activation:
  positive:
    - { key: schema-change, desc: "Ticket implies schema/migration changes." }
    - { key: api-design, desc: "Ticket adds or changes a public API surface." }
  negative:
    - { key: pure-refactor, desc: "No external behavior change." }

role_assignment:
  lead:
    positive:
      - { key: architectural-impact, desc: "Decision crosses module boundaries." }
  analyst:
    positive:
      - { key: needs-perspective, desc: "Decision benefits from architectural lens." }
  reviewer:
    positive:
      - { key: architectural-impact, desc: "Reviewer compares against architect's plan." }

references:
  - { id: solid, path: "references/solid.md" }

output_contract:
  schema_ref: skill-observation.schema.json
  hard_limits: { max_observations: 8, max_chars_per_field: 500 }
  output_type:
    lead: plan
    analyst: list
    reviewer: list
---

## as_lead

You are the architect leading the plan. Decide module boundaries, dependency direction,
and which existing patterns to follow. Reference {{ref:solid}} when relevant.

## as_analyst

You are the architect contributing perspective on architectural fit. No veto.

## as_reviewer

You are the architect reviewing the diff against the plan: {{plan}}.

Confirm the plan was followed; flag deviations with evidence (file:line references).
```

## What changed

- `roles_supported` declares which `SkillRole` values triage may assign.
- `activation` controls whether the skill participates at all (positive AND no negative match).
- `role_assignment.<role>` controls per-role activation. Triage walks roles in `roles_supported` and assigns the role iff its positive matches AND no negative matches.
- `references` lets the skill body cite supplementary content via `{{ref:<id>}}`. The body resolver inlines them at prompt-build time.
- `output_contract.output_type[<role>]` declares the expected output shape per role: `list` (observations), `plan` (lead's structured plan), `artifact` (synthesized text).
- The body splits into `## as_<role>` sections. The role-specific section is rendered when triage assigns that role; skills without per-role sections fall back to the whole body.
- `{{plan}}` in a `## as_reviewer` section is substituted with the Plan-phase Lead's `PlanArtifact` at runtime. Reviewers without a same-run lead see `(no plan provided)`.

## What's gone

- `agentsmith.md` `## orchestration` section is no longer read. `SkillOrchestrationParser` is removed.
- The `OrchestrationRole` enum (`Lead`, `Contributor`, `Gate`, `Executor`) and the `runs_after` / `runs_before` graph are replaced by `SkillRole` (`Lead`, `Analyst`, `Reviewer`, `Filter`) and explicit phases.
- Legacy graph builder (`SkillGraphBuilder`, `DeterministicTriageBuilder`) is deleted. Triage now produces phase-keyed assignments via the LLM, not topological sort over orchestration metadata.

`agentsmith.md` survives for discussion-pipeline-only fields (`display-name`, `emoji`, `triggers`, `convergence_criteria`).

## Strict mode

`coding/` and `security/` skill categories reject `SKILL.md` files lacking `roles_supported` at boot with a migration hint. Discussion categories (`legal/`, `mad/`) are exempt — they default to `roles_supported=[Analyst]`.
