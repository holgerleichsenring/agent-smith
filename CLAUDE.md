# agent-smith — AI Agent Instructions

## Context Files (read in this order)

1. **Every** `.agentsmith/contexts/<name>/context.yaml` (glob `contexts/*/context.yaml`) — architecture, stack, integrations, phase status PER stack
2. **Every** `.agentsmith/contexts/<name>/principles.md` — code quality rules per stack (ALWAYS follow)
3. `.agentsmith/phases/active/*.yaml` — spec for the phase being implemented (its `applies_to:` names the dominant context)
4. `.agentsmith/decisions/*.yaml` — past decisions, one YAML per phase or run (read the active phase's file and its `requires:` chain)
5. `.agentsmith/memory/MEMORY.md` — the experiential-memory index, one line per recorded fact; recall detail from `.agentsmith/memory/<name>.md` on demand when a line touches your task

## Phase Directory Structure

```
.agentsmith/phases/
  done/       # completed phases (historical reference)
  active/     # phase currently being worked on
  planned/    # upcoming phases with requirements
```

## Experiential Memory (remember / recall)

`.agentsmith/memory/` holds typed Markdown facts: one file per memory with
frontmatter `name` (slug = filename), `description` (one line), and
`metadata.type` (`feedback` = how the operator wants the agent to work,
ratification required; `project` = goals/constraints/state not derivable from
code or git; `reference` = external pointers). `MEMORY.md` is the index —
one line per memory, content never in the index.

- **Recall before re-deriving**: when the index hints at a fact you are about
  to work out from scratch, read the entry file instead.
- **Remember sparingly**: store what code and git cannot already tell the
  next agent — one fact per file; check the index for an existing entry and
  update rather than duplicate; delete a memory that turns out wrong; link
  related memories as `[[slug]]` (a cited slug requires its committed
  definition). A new or changed `feedback` entry is a PROPOSAL until the
  operator ratifies it.
- **Memory vs decision**: a decision records a CHOICE (`decisions/`); a
  memory records a transferable FACT or RULE. Never duplicate one into the
  other.

## Minting a Phase Id

A phase id is minted from the clock, never from a count: **today's UTC date plus four
random hex digits** — `{yyyy-MM-dd}-{xxxx}`, e.g. `2026-08-24-8a3f`. Read the date off
the machine you are on and take the four digits at random. Nothing else is consulted.

Minting therefore needs no knowledge of what anyone else has taken. A worktree cut this
morning, a sandboxed agent with no network, and two agents working in parallel all mint
safely — the four hex digits give a 16-bit keyspace against a same-day collision.
"What is the highest number so far?" is a question none of them can answer, and
answering it wrongly is how two phases end up sharing one id.

The suffix's **fixed width** marks where the id ends and the label begins:
`.agentsmith/phases/planned/2026-08-24-8a3f-phase-id-offline-minting.yaml`.

**Counter ids (`p0042`, `p0057a`, `p0131c-pre`) are a closed namespace.** Every one of
them stays valid forever and an id is never renamed. The namespace is closed to NEW ids
only. Counter ids run four to six digits, six because ids minted from a ticket number
(`p19106a`) live in the same namespace.

**An id is frozen; a file name is a pointer.** Every `requires:` edge, every record line
and every commit message cites the ID, which is why moving a phase file breaks nothing but
the `-> .agentsmith/phases/…` pointer in `context.yaml` — and that pointer moves with the
file (`PhaseRecord_EveryPointer_ResolvesToAFile` proves every one resolves). Phases from
p0400 upward and every date-minted phase were relabelled once, in 2026-09-07-4e6a; the
cut is where the measurement turned (mean slug words 4.0 over p0350–p0399, 5.8 over
p0400–p0449, 8.2 over p05xx, 7.6 over the dated phases). That was a one-off migration
boundary, not a rule: the RULE is scoped by namespace and shape, never by an ordering,
because a date-minted id sorts below every counter id as text.

A deferred successor is named in prose by what it does — a random suffix cannot be
reserved in advance, so `requires:` names only phases that already exist.

## Naming a Phase

A phase file is `{id}-{label}.yaml`. The label is a **topic label of 2 to 5 words**, at
most **50 characters**, and the `goal:` is one sentence of at most **200 characters**. The
CLAIM lives in the goal — it already states it, with room, punctuation and grammar that a
fifty-character slug has none of. The reasoning goes in `decisions:`.

The label is **area-first**: the leading word names the subject area, the rest narrows it —
`checkpoint-partial-restore`, `account-base-ref-search`, `scope-refusal`,
`handback-question-case`, `derivation-read-only-tools`. Kin share the leading word, so a
directory listing groups them and `ls phases/done/account-*` finds them without a shared id
prefix. A label for a counter id must not begin with `pre`, which the id regex would swallow
as a `-pre` tail.

**Labels may repeat; the id is the identity.** Two phases on one topic may carry one label
— the goal says which is which — so uniqueness is not enforced. A label is not a sentence:
`the-diagram-cannot-lie` and `one-gate-not-two` were relabelled to
`flow-diagram-evidence` and `gate-removed`. A relabel of a phase whose record entry is
already over the 400-character cap may not lengthen the entry — the pointer is part of it.

`PhaseNameRuleTests` enforces the word ceiling and the character bound over the DATE-MINTED
namespace; the closed counter namespace is out of scope by construction, and the scoping is
a namespace rather than an ordering. The product still mints a sentence slug from the goal
(`PhaseIdFactory.Slug`, and a second one in `WritePhaseRecordHandler`) — bounding it is
`2026-09-07-e9a2` (phase-slug-product-bound).

## Implementation Workflow (follow this order for every phase)

1. **Write phase spec first** — create `.agentsmith/phases/planned/{id}-label.yaml` with goal, `applies_to:`, steps, and definition of done BEFORE writing any code. No exceptions. Mint `{id}` per **Minting a phase id** below.
2. **Move to active** — move the phase file from `planned/` to `active/` when starting work.
3. **Plan first** — explore codebase, design approach, get user approval before coding.
4. **Implement step by step** — contracts/models first, then implementation, then wiring, then tests.
5. **Build after each step** — fix errors immediately, don't accumulate them.
6. **Run ALL tests** — ensure zero failures before moving on.
7. **Log decisions** — one YAML per phase at `.agentsmith/decisions/{id}.yaml`; each entry: what was chosen, what alternatives existed, and why.
8. **Update state** — move phase from `planned`/`active` to `done` in the relevant context's `context.yaml`. The `state.done` entry is an INDEX LINE, **max 400 characters** and enforced by `PhaseRecordLengthRatchetTests`: what shipped, in what area, and the `-> .agentsmith/phases/done/…` pointer. The reasoning goes in the spec the pointer names and in `decisions/{id}.yaml` — an entry that repeats its spec is a second copy that will disagree with the first.
9. **Move to done** — move the phase file from `active/` to `done/`.
10. **Commit** — one commit per phase, descriptive message.

## Phase Gate — what it covers

`.claude/hooks/phase-gate.sh` runs as a PreToolUse hook on every Bash call and gates a
`git commit` whose message names a phase: the dashboard's own build and tests, the
backend build, the full suite, CLI dry-runs and harness presets must be green, or the
commit is blocked. Read this before a definition of done leans on it.

- **The dashboard runs first.** `pnpm install --frozen-lockfile`, `pnpm test` and
  `pnpm build` in `src/dashboard`, before any .NET check — its own CI workflow is
  path-filtered on `src/dashboard/**`, so a backend-only payload change never proved the
  half that renders it. A tree with no `src/dashboard/package.json` has nothing to check
  and says so; a tree that has one and no `pnpm` **blocks**, because a silent skip is
  indistinguishable from a pass.

- **One copy serves everyone.** `$CLAUDE_PROJECT_DIR` is the launching session's project
  directory, so a subagent in its own worktree runs the *shared checkout's* script — an
  edit to the gate takes effect only once it lands in that working tree. It still gates
  the tree the commit runs in, resolved from the call's `cwd` or a leading `cd`.
- **Gated message forms:** `-m`, repeated `-m`, a heredoc, `-F <file>`, `-t <template>`,
  `-C`/`-c <rev>`, `--amend --no-edit`.
- **Passed through, loudly:** a bare commit or an editor amend (no message exists yet),
  `-F -`, and a message the shell builds from a command substitution such as
  `-m "$(cat message.txt)"` — its text never reaches the gate. Run the checks by hand.
- **Not covered at all:** a commit made outside the Bash tool.
- **Afterwards:** every recognised phase commit leaves one line in `.claude/phase-gate.log`
  of the shared checkout — verdict (`passed`/`blocked`/`not-gated`), phase id, the tree, and
  the commit's parent. A phase commit with no line never met the gate.

## Key Rules

- **English only** — all code, comments, docs, exceptions, logs, commit messages. Phase specs and repo docs are English even when the conversation is German.
- **No customer names** — never write customer, project, or target identifiers into any artifact in this repo (see `[[feedback_no_customer_names]]`).
- **No over-engineering** — only build what the phase requires, nothing more.
- **Tests** — every new public method gets at least one test.
- **Follow each context's principles.md** — these are constraints, not suggestions.
