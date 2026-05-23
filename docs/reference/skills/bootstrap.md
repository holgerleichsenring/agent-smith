# Bootstrap Skills Reference

The `init-project` pipeline runs exactly one **bootstrap skill** against the
analyzed repository. That skill writes `.agentsmith/context.yaml` and
`.agentsmith/coding-principles.md` — the two files every code-touching
pipeline (`fix-bug`, `add-feature`, `security-scan`, ...) requires.

Which bootstrap skill runs is decided by the `project_language` enum, derived
from `ProjectAnalyzer`'s detected primary language.

## The `project_language` enum

The enum has four values. The mapping from the free-form
`ProjectMap.PrimaryLanguage` string to the enum is a fixed code table
(`ProjectLanguageMapper`) — adding a fifth language is a vocabulary change,
not a config knob.

| `project_language` | Synonyms recognized in `primary_language` |
|---|---|
| `csharp` | `csharp`, `c#`, `.net`, `dotnet` |
| `node` | `typescript`, `javascript`, `node`, `node.js`, `nodejs`, `ts`, `js` |
| `python` | `python`, `py` |
| `generic` | _any value not listed above, or empty/missing_ |

Matching is case-insensitive. The detected `primary_language` is preserved
verbatim in `ProjectMap` — only the enum collapses.

## The four bootstrap skills

Each bootstrap skill declares its activation in `SKILL.md` frontmatter. The
expression is evaluated by the skill loader at run time.

| Skill | `activates_when` |
|---|---|
| `csharp-bootstrap` | `pipeline_name = "init-project" AND project_language = "csharp"` |
| `node-bootstrap` | `pipeline_name = "init-project" AND project_language = "node"` |
| `python-bootstrap` | `pipeline_name = "init-project" AND project_language = "python"` |
| `generic-bootstrap` | `pipeline_name = "init-project" AND project_language = "generic"` |

The `generic-bootstrap` skill is the fallback for everything outside the
narrow enum (Go, Java, Kotlin, Rust, Ruby, Elixir, Swift, ...). Its output is
deliberately minimal and flagged in `context.yaml` as a stub that the
operator is expected to flesh out before merging the bootstrap PR.

## Diagnosing language misclassification

If a TypeScript monorepo gets bootstrapped as `generic`, or a multi-language
repo picks the wrong language:

1. Open the init run's `result.md` under `.agentsmith/runs/<run-id>/`. It
   reports the analyzer's `primary_language` value and the resolved
   `project_language` enum side by side.
2. If `primary_language` is correct but the enum collapsed to `generic`,
   the synonym is missing from the mapper table — file an issue.
3. If `primary_language` itself is wrong, `ProjectAnalyzer`'s detection
   heuristics need adjustment. The most common cause is a vendored
   third-party directory swaying the file-count heuristic; exclude it via
   the repo's `.gitignore` or analyzer ignore rules and re-run `init-project`.

The PR body of a bootstrap run names the skill that ran, so you can confirm
the dispatch decision without reading the run log.
