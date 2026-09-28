# Bootstrap Skills Reference

The `init-project` pipeline writes, for every component of a repository, a
`.agentsmith/contexts/<name>/context.yaml` and a `principles.md` beside it: the
files every code-touching pipeline (`code`, `security-scan`, ...) requires. What
goes into `context.yaml` is described on [The context file](../concepts/context-file.md);
this page is about how init gets there.

## One master, one round per component

The work is done by one master skill from the catalog, `project-bootstrap`, which
activates on `pipeline_name = "init-project"`. There is no per-language bootstrap
skill.

The pipeline runs in three moves:

1. **`AnalyzeCode`** reads the repository and publishes its primary language, as the
   analyzer names it (trimmed, lower-cased), as the `project_language` concept. A
   missing or empty language fails the run rather than being passed off as
   "generic".
2. **`BootstrapDiscover`** enumerates the repository's components, read-only. A
   component is something built and shipped on its own. The internal layer projects
   of one solution (`Domain`, `Infrastructure`, …) belong to the component that
   ships them.
3. **`BootstrapDispatch`** runs one bootstrap round per (repo, component). Each round
   runs on the agent's `context_generation` model, reads that component's subtree
   through tools, and writes that component's `context.yaml`, including `stack.image`
   and a `verify` block derived from the CI pipeline the repository already runs.

## Principles are transferred, not inferred

`principles.md` is not written from what the code happens to look like. The framework
composes it from two files in the skills catalog and places it before the round runs:

- `principles/core.md`: the universal intent (SOLID, DRY, YAGNI, Tell-Don't-Ask, …),
  the same for every language;
- `principles/deltas/<language>.md`: the mechanisms that realise that intent in one
  language (naming, layout, tooling). The catalog currently ships deltas for `csharp`,
  `typescript` and `rust`.

Two repositories of the same stack get byte-identical principles and differ only in
what their operators ratified. A language with no delta gets the core alone, with a
line saying so. A catalog that ships no core at all hands authorship back to the
bootstrap skill, and the init PR says which of the two happened.

A delta may also declare files for the repository (the artefacts). The round writes
them next to the principles, and the PR body lists each one as written, already
present, or not written with the reason.

## What survives a re-init

`principles.md` is named for what it holds. Rules about the **environment** count as
much as rules about code, and their home is its **Project Specifics** section:
"field changes go through the estate's own CLI", "hand-written SQL in a model is a
defect". An existing `principles.md` is never overwritten, so anything the operator
ratified in the init pull request holds from then on.

A repository initialised under the old file name `coding-principles.md` has it renamed
to `principles.md` before the round looks, so the ratified content is found and kept.
Until then, code-touching pipelines refuse the repository and name re-init as the
remedy.

`context.yaml` behaves differently: a re-init derives it again, so a wrong value is
corrected, while sections the typed document does not model are carried over. A
context the derivation no longer produces is moved to
`.agentsmith/contexts-retired/<name>/`. See [Onboarding](../setup/onboarding.md).

## Diagnosing a wrong result

- **The components are wrong** (two contexts where you expected one, or the reverse):
  the discovery round decided it. Re-run `init-project`; it derives from scratch and
  retires what it no longer produces. If it keeps deciding the same way, the
  repository's layout is what it reads, so that is where to look.
- **The principles carry no delta for your language**: the catalog has none for it
  yet. The core still applies; add the mechanisms under Project Specifics, or
  contribute a delta to the [skills repository](https://github.com/holgerleichsenring/agent-smith-skills).
- **The run failed before any round**: open the init run's `result.md` under
  `.agentsmith/runs/<run>/`, or the run in the dashboard. A missing primary language
  or a missing `project-bootstrap` master in the resolved catalog is named there.
