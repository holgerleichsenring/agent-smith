# What the adversarial review changed, 2026-09-20

Twelve phases were drafted over two days against one goal: that a run inside
agent-smith should work the way a careful engineer works — discuss, read the
code, declare a spec, have it attacked, correct, implement, review the
implementation, commit per phase. Nine were built and merged. Two were refused
at the root and are withdrawn here. One is still under review. What follows is
what the review falsified, so the same ground is not walked twice.

## Withdrawn

**2026-09-20-8d4a, "a design turn leads with the decision it needs".** Its
premise was that the design partner buries the one thing only the operator can
settle under a survey of findings. It does not bury it: `design-partner-master`
carries a "Discussion comes first" section, shipped in skills #191, which
prescribes exactly that answer shape — what you found, edge cases, open
questions — in exactly that order. The long reply the spec was written against
is the rule working, not the rule missing. The spec also rested on a single
observed turn in which the operator engaged with none of the findings; asked
directly, the operator said they were satisfied with the reply. An inversion
of a shipped, deliberate ordering needs evidence that the ordering is wrong,
and one turn read as impatience is not that. Revisit only with a measurement
over several turns — for instance, how often the decision the operator actually
makes was raised in the turn before the one that raised it.

**2026-09-20-5e1b, "every named successor is listed where someone looks".** Its
goal cited thirty-three successors named in prose. That figure was an artifact:
it was produced by grepping every worktree in the checkout, so specs present in
nine worktrees were counted nine times. A single-tree count is given below and
is an order smaller for the phrase the spec was built on. The review's surviving
recommendation was that this is a QUERY, not a committed artifact: a generated
file held by a test buys nothing a one-line search does not, and adds a file
that must be regenerated on every spec that lands. Revisit only if the query
turns out to be run often enough that a person forgets it exists.

## The count, and how to reproduce it

Three prose forms defer work to a later phase. Flattening whitespace matters:
these sentences wrap inside YAML block scalars, so a line-oriented grep finds a
fraction of them. Over ONE tree, `.agentsmith/phases/**` plus
`.agentsmith/decisions/**`, with `\s+` collapsed to a single space:

| phrase | phases | decisions |
|---|---|---|
| `named in prose as the phase that` | 13 | 1 |
| `its own phase` | 37 | 27 |
| `a separate phase` | 24 | 6 |

Not every hit is a deferred successor — `its own phase` also appears in prose
that is not a deferral — so these are an upper bound on the backlog, not a
count of it. `planned/` holds 98 specs besides.

Three different figures for this were stated during the session before this one
was measured. Any number quoted from a multi-worktree checkout is wrong by the
number of worktrees.

## Falsified premises worth keeping written down

- **A reviewer's look cannot be given a shell.** The first cut of
  2026-09-20-9c74 would have handed a model `/bin/sh -c` inside the run's real
  toolchain sandboxes — containers that carry registry credentials under the
  home directory and whose working tree the commit path later commits from.
  Read-only is a property of what CAN be run, not of what the caller is asked
  to stick to.
- **The design turn's look could not have used it anyway.** Its source scopes
  refuse a run step on the first line of the step handler, before anything
  materialises, and a passing test already asserts that refusal.
- **A spec written from screenshots and memory is refused at the root.** Seven
  specs written from the code took four to eight cuts each and all seven were
  accepted. Five written the same night from screenshots were refused on the
  first attack — premise false, mechanism inert, one of them unsafe. The
  refusal COUNT is not the signal; the refusal KIND is. "Your line number is
  wrong" is the process working. "The thing you built this on does not exist"
  is the process being skipped.
- **A reviewer cannot see an uncommitted spec.** Five specs were written as
  untracked files in one worktree while the review worktrees were cut from the
  branch. Every reviewer reported the file missing.
- **A rewritten decision keeps its old heading.** When a decision is rewritten
  against a finding, the edit touches the body and leaves the heading and both
  `scope` halves stating the position that was discarded. One file carried
  four such contradictions. The heading is what gets skimmed and what gets
  quoted into a record entry.
