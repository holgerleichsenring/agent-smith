# Skills Reference

Skills declare specialist AI roles that participate in pipeline runs. Each skill is a directory under the configured skills root with two files:

- `SKILL.md` — frontmatter (declarative metadata) + body (per-role prompts).
- `agentsmith.md` — *optional*, discussion-pipeline-only fields (display name, emoji, triggers, convergence criteria). The legacy `## orchestration` section is no longer read.

`SKILL.md` frontmatter is the single source of truth for orchestration metadata as of phase p0111. See [migration guide](skills/migration.md) for before/after.

## Directory layout

```
<skills-root>/
├── _index/                       # auto-generated, gitignored
│   ├── coding.yaml
│   └── security.yaml
├── concept-vocabulary.yaml       # operator-extensible vocabulary
├── coding/                       # used by fix-bug, add-feature
│   ├── architect/
│   │   ├── SKILL.md
│   │   └── references/...
│   └── tester/
├── security/                     # used by security-scan
├── api-security/                 # used by api-security-scan
├── legal/                        # discussion pipeline
└── mad/                          # discussion pipeline
```

Categories `coding/` and `security/` are **strict mode** — `SKILL.md` files lacking `roles_supported` are rejected at boot with a migration hint. `legal/` and `mad/` are exempt; they default to `roles_supported=[Analyst]`.

## SKILL.md frontmatter

Full schema:

```yaml
---
name: architect                  # unique within the skills root
display_name: Architect
emoji: 🏛️
description: System architect who plans architectural changes.

roles_supported: [lead, analyst, reviewer]   # which SkillRoles triage may assign

activation:                       # whether the skill participates at all
  positive:
    - { key: <vocab-key>, desc: "Why this key activates the skill." }
  negative:
    - { key: <vocab-key>, desc: "Why this key suppresses the skill." }

role_assignment:                  # per-role activation
  lead:
    positive:
      - { key: <vocab-key>, desc: "Why this skill leads in this case." }
    negative:
      - { key: <vocab-key>, desc: "Why this skill should not lead." }
  analyst:
    positive: [...]
  reviewer:
    positive: [...]

references:                       # citable from body via {{ref:<id>}}
  - { id: solid, path: "references/solid.md" }

output_contract:
  schema_ref: skill-observation.schema.json
  hard_limits:
    max_observations: 8
    max_chars_per_field: 500
  output_type:                    # per-role expected output shape
    lead: plan
    analyst: list
    reviewer: list
    filter: list                  # or "artifact"
---
```

`activation` and `role_assignment` keys must exist in the project's [concept vocabulary](concept-vocabulary.md). `ConceptVocabularyValidator` warns at boot for unknown keys (it does not fail).

## Body — per-role sections

The body splits into `## as_<role>` sections:

```markdown
## as_lead

Instructions for when this skill is assigned the Lead role.

## as_analyst

Instructions for the Analyst role.

## as_reviewer

Compare the implementation against the plan: {{plan}}.

Cite references with {{ref:solid}}.

## as_filter

Instructions for the Filter role (output: list = reduce; artifact = synthesize).
```

Skills without per-role sections fall back to the whole body for any assigned role — used by legacy skills under `legal/` and `mad/`.

The body is lazily resolved by `SkillBodyResolver`:

- The `## as_<role>` section matching the assigned role is selected (or the full body if none).
- `{{ref:<id>}}` placeholders are inlined from the file at the matching `references[].path`.
- `{{plan}}` (in `## as_reviewer` sections) is substituted with the run's `PlanArtifact`. Reviewers without a same-run lead see `(no plan provided)`.

The resolver caches per `(skill, role)` for the process lifetime; references are read once.

## Output contract

`output_contract.output_type[<role>]` declares what the LLM is expected to return for that role:

| Value | Returned shape |
|---|---|
| `list` | JSON array of `SkillObservation` objects. |
| `plan` | Single structured plan object that reviewers compare against (Lead's typical output). |
| `artifact` | Free-form text (Filter mode for synthesis, e.g. final security report). |

`hard_limits.max_observations` caps how many observations the LLM may emit. `max_chars_per_field` caps each string field. Both are enforced by parsers downstream.

## Per-provider overrides (planned, p0111d)

A future phase introduces optional `SKILL.<provider>.md` files that replace the base `SKILL.md` when the active provider matches (e.g. `SKILL.openai.md` overrides for OpenAI deployments). The mechanism is opt-in per skill, opt-in per operator. Zero overrides ship initially. Authoring a `SKILL.<provider>.md` requires the override to declare the same `name` and `roles_supported` as base; mismatches are rejected with a clear error.

## Index files

`SkillIndexBuilder` writes a compact projection of each category's loaded skills to `<skills-root>/_index/<category>.yaml` at boot. The index is what the triage step reads; the LLM never sees the full body content of unassigned skills.

Read-only filesystems trigger a warning, not a crash — the index is best-effort cache, and the framework falls back to in-memory aggregation per process when writes fail. Operators who mount skills read-only can set `SkillsConfig.IndexPath` to a writable location.
