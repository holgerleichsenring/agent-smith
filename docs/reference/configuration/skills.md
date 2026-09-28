# Skills reference

!!! note "Where these files live"
    Skills are files in the skill catalog, not configuration: every release ships its catalog embedded, and the `skills:` settings block only points somewhere else for skills development, an air-gapped mirror or an overlay of your own skills. Nothing on this page is stored in the database or edited in the Config studio. See [Skills catalog](../../how-it-works/skills-catalog.md).

A skill is a directory with a `SKILL.md` file: YAML frontmatter that says what the skill is, followed by the prompt body. This page describes the format the loader accepts. For how a master uses its tools and child agents at run time, see [Multi-agent orchestration](../concepts/multi-agent-orchestration.md).

## Where the catalog comes from

With no `skills:` block, Agent Smith unpacks the catalog embedded in the binary. `skills.version`, `skills.url` or `skills.path` replace it with a pinned release, a tarball or a mounted directory. `skills.overlay` layers a directory of your own on top of whichever catalog was resolved: both trees are copied into one root, file by file, with the overlay's files winning. An overlay must contain a `skills/` subdirectory and needs `skills.cache_dir` set, because the layered catalog is written next to it. See [Skills catalog](../../how-it-works/skills-catalog.md) and [Shipping your own skills](../skills/your-own-skills.md).

## Catalog layout

```
<catalog-root>/
├── skills/
│   ├── concept-vocabulary.yaml       # concepts activates_when may reference
│   ├── description-cap.txt           # the description cap the catalog's release gate enforces
│   ├── _masters/
│   │   ├── coding-agent-master/
│   │   │   └── SKILL.md
│   │   ├── security-master/
│   │   │   └── SKILL.md
│   │   └── ...
│   └── <skill-name>/                 # a non-master skill
│       └── SKILL.md
├── references/                       # shared text a body cites with {{ref:<name>}}
├── principles/                       # core.md + deltas/<language>.md
└── patterns/                         # static-pattern rules for security-scan
```

The loader reads every `skills/<name>/SKILL.md` and every `skills/_masters/<name>/SKILL.md`. Other directories whose name starts with `_` are skipped. The shipped catalog keeps everything under `_masters/`: the pipeline masters, plus `project-discovery` and `project-bootstrap`, the two producers that `init-project` runs.

## SKILL.md frontmatter

```yaml
---
name: security-master
description: "Master loop for the security-scan pipeline. Runs a code-security methodology over repo source and scanner outputs to emit prioritized findings."
role: master
version: "1.7.0"
output_schema: observation
---
```

| Key | Required | Meaning |
|---|---|---|
| `name` | yes | The skill's id. A master is looked up by this name. A file without a `name` is skipped. |
| `role` | yes | One of `producer`, `investigator`, `judge`, `filter`, `master`. A file without a `role` is rejected. |
| `description` | yes | One line, at most 200 characters. See [Description limit](#description-limit). |
| `output_schema` | yes, except for masters | One of `observation`, `plan`, `diff`, `bootstrap`, `discovery`. On a master it is optional; `observation` gives the master the read-only scan surface. |
| `activates_when` | yes, except for masters | An expression over the [concept vocabulary](concept-vocabulary.md) that decides whether the skill takes part, for example `pipeline_name = "init-project"`. |
| `investigator_mode` | when `role: investigator` | One of `verify_hint`, `survey`, `verify_diff`. |
| `survey_scope` | when `investigator_mode: survey` | A non-empty list. |
| `category` | when `investigator_mode: verify_hint` | A non-empty string. |
| `block_condition` | when `role: judge` and `output_schema` is not `observation` | When the judge blocks. |
| `version` | no | The skill's own version string. |
| `display_name`, `emoji`, `triggers`, `allowed_tools`, `scope_hint`, `loop` | no | Optional metadata. |

`output_schema: bootstrap` is only valid with `role: producer`. The loader ignores keys it does not know, so a `metadata:` block in a shipped `SKILL.md` does no harm.

A master only needs `name`, `role`, `description` and a non-empty body. Every other skill also needs `activates_when` and `output_schema` and the role-specific keys above.

A skill that breaks one of these rules is dropped when the catalog loads, and the log names the file and the rule. During a run the drop also shows up as a catalog warning on the run.

## Description limit

`description` must be a single-line scalar of at most 200 characters. A YAML block scalar (`|` or `>`) is rejected, because its length reads differently to the loader than to the catalog's release gate. The same rule runs in two places: when the embedded catalog is packaged into a build, and every time a catalog is loaded. The catalog records the cap it enforces in `skills/description-cap.txt`, and a build refuses a vendored catalog whose number differs.

The limit matters most for masters. A master whose description is too long is dropped at load time, and the run that needs it fails later, when it asks the catalog for that master's prompt. Keep master descriptions well below the cap.

## Body

Everything after the closing `---` is the prompt, used as it stands. There are no per-role sections. Two kinds of placeholder are filled in before the body reaches the model.

`{{ref:<name>}}` inlines `references/<name>.md` from the catalog root. The name is lower-case words joined by hyphens. A reference cannot cite another reference, and a citation of a file the catalog does not ship fails the prompt rather than sending it without the text.

A master body can also use the master prompt tokens, in single braces: `{ProjectContextSection}`, `{CodingPrinciples}`, `{CodeMapSection}`, `{RepoNames}`, `{PlanSection}`, `{RunRecordDir}`, `{MaxFixIterations}`, `{ExpectationSection}`, `{SpecSection}`, `{ProgressLedgerSection}`, `{MemoryIndexSection}` and `{PrDiffSection}`. The step that runs the master supplies the values. A known token that the step does not fill fails the render. Braces outside this set, such as `/users/{id}` in an API example, are left alone.

## Optional companion files

A skill directory may carry two more files next to `SKILL.md`:

- `agentsmith.md` sets `display-name`, `emoji`, `triggers` and `convergence_criteria`.
- `source.md` records where a skill came from, with `origin`, `version`, `commit`, `reviewed` and `reviewed-by`.

## Per-provider overrides

A `SKILL.<provider>.md` file in a skill directory replaces the base `SKILL.md` when `primary_provider` at the root of `agentsmith.yml` names that provider. With `primary_provider` unset, the base file is always used.

```
<catalog-root>/skills/_masters/coding-agent-master/
├── SKILL.md            # base
└── SKILL.openai.md     # used when primary_provider: openai
```

The override's frontmatter is merged key by key over the base: a key the override sets replaces the base value, a key it leaves out keeps the base value. The body comes from the override file. The override's `name` must match the base, and so must its `role` when it sets one; a mismatch rejects the skill. When an override loads, the log says `Provider override loaded for skill '<name>' from <path>`.

The shipped catalog contains no overrides. Put your own in an overlay.
