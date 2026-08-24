# agent-smith — AI Agent Instructions

## Context Files (read in this order)

1. **Every** `.agentsmith/contexts/<name>/context.yaml` (glob `contexts/*/context.yaml`) — architecture, stack, integrations, phase status PER stack
2. **Every** `.agentsmith/contexts/<name>/coding-principles.md` — code quality rules per stack (ALWAYS follow)
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

The suffix's **fixed width** marks where the id ends and the slug begins:
`.agentsmith/phases/planned/2026-08-24-8a3f-a-phase-id-can-be-minted-offline.yaml`.

**Counter ids (`p0042`, `p0057a`, `p0131c-pre`) are a closed namespace.** Every one of
them stays valid forever and none is ever renamed — every `requires:` naming one, every
record entry and every commit message citing one keeps working exactly as it did. The
namespace is closed to NEW ids only. Counter ids run four to six digits, six because
ids minted from a ticket number (`p19106a`) live in the same namespace.

A deferred successor is named in prose by what it does — a random suffix cannot be
reserved in advance, so `requires:` names only phases that already exist.

## Implementation Workflow (follow this order for every phase)

1. **Write phase spec first** — create `.agentsmith/phases/planned/{id}-slug.yaml` with goal, `applies_to:`, steps, and definition of done BEFORE writing any code. No exceptions. Mint `{id}` per **Minting a phase id** below.
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
`git commit` whose message names a phase: build, full suite, CLI dry-runs and harness
presets must be green, or the commit is blocked. Read this before a definition of done
leans on it.

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
- **Follow each context's coding-principles.md** — these are constraints, not suggestions.
