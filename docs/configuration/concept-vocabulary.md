# Concept Vocabulary

The concept vocabulary maps freeform project signals (frameworks, modules, tech stack) onto a stable set of named concepts that skills' activation criteria reference. It is loaded once at boot from `<skillsDirectory>/concept-vocabulary.yaml` and is operator-extensible.

## File format

```yaml
project_concepts:
  persistence:
    desc: "Project has a persistence layer (database, ORM)."
  api-server:
    desc: "Project exposes an HTTP API."
  frontend:
    desc: "Project has client-side rendering or a UI."
  containerized:
    desc: "Project ships container images."

change_signals:
  schema-change:
    desc: "Ticket implies database schema or migration changes."
  security-fix:
    desc: "Ticket calls out a security issue."

run_context:
  pre-execute:
    desc: "Skill runs before the developer agent (Plan phase)."
  post-execute:
    desc: "Skill runs after the developer agent (Review phase)."
```

Three sections, one flat key namespace. Duplicate keys across sections are an error caught by `ConceptVocabularyLoader`. Each entry is `{ key, desc, section }`.

## How it's used

`ProjectMapExcerptBuilder` builds a signal-text from the project's `ProjectMap` (primary language, frameworks, module paths, test/CI signals) and includes any vocabulary key whose name appears as a substring. The matching is intentionally crude — a key like `persistence` matches when "Persistence" appears in a module path or when an EntityFramework framework is detected. Operators sharpen the result by extending the vocabulary, not by editing matcher logic.

The matched concept list lands in `ProjectMapExcerpt.Concepts` and is rendered into the triage prompt under `## Project Map Excerpt`. Skills' activation criteria reference these concept keys.

## Extending

Add new concepts to your project's `concept-vocabulary.yaml` — they pick up signal-text matches automatically on the next pipeline run. There is no code change required.

`ConceptVocabularyValidator` warns (does not fail) when a skill's `activation.positive` references an unknown concept key. The warning carries the skill name and the unknown key so operators can see at boot what's missing from their vocabulary.


## Validation (build-time / CI)

`agent-smith validate-concepts --skills-path <skills-dir>` exercises three cross-cuts at build-time without running a pipeline:

- **`activates_when` references the vocabulary.** Every concept name appearing in a `SKILL.md` `activates_when` boolean expression must exist in `concept-vocabulary.yaml`. References to undeclared concepts are caught here, not in production triage.
- **Handlers vs vocabulary writers (forward).** Every concept declared by an `IConceptWriter` (handlers like `CheckoutSourceHandler`, `BootstrapCheckHandler`, `PipelineNameInitializerHandler`) must be in the vocabulary, must have the matching `type`, and must list the handler in its `writers:` array.
- **Vocabulary writers vs handlers (reverse).** Every name in a concept's `writers:` list must be a registered `IConceptWriter`. Empty `writers:` lists are allowed (some concepts are seeded by tests or future external publishers).

Output is one error per line in the form `<subject>: <concept>: <message>`, sorted by subject then concept. Exit code is `0` on a clean tree, `1` on any error.

Run locally before committing vocabulary or handler changes; wire into CI once `activates_when` ships in real skills (D3 / p0127). For unparseable expressions the verb prints the offending offset and token from the activation expression parser (p0125b), so operators can fix the SKILL.md frontmatter directly.

```bash
$ agent-smith validate-concepts --skills-path agent-smith-skills/skills
api-vuln-analyst: pipeline_namee: activates_when references concept not declared in vocabulary
CheckoutSourceHandler: source_available: handler declares type Int but vocabulary declares Bool
```

