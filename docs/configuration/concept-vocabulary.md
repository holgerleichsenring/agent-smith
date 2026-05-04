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
