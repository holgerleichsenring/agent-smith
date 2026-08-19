# Built by the method it teaches

Agent Smith bootstraps an `.agentsmith/` directory into your repository: a context
file, phase specs, a decision log, a memory of what it learned. That is the
product. It is also how this repository itself got written, over six months, by
one person and one language model.

This page is the account. Every number on it can be reproduced from a clone, and
the counting method is at the bottom.

## The numbers

| | |
|---|---|
| **610** | completed phases, each specified before a line of code existed |
| **2,587** | recorded decisions, each naming the alternative it beat |
| **244,753** | lines of C# across 3,044 files in 31 projects |
| **3,760** | automated tests, gating every single commit |
| **1,799** | commits, 350 merged PRs, 185 releases |
| **~500 h** | of human time, roughly 50 minutes per completed phase |

Period: 18 February to 19 August 2026, so 182 calendar days. One human author,
one model.

Two ratios say more than the totals. There are 245 lines of specification for
every 1,000 lines of code, which makes the spec about a quarter of the written
work. And there is 0.65 of a line of test code for every line of production
code. The test suite does most of the steering here, so that ratio is a design
choice rather than an accident.

## The unit of work is the phase

No sprint board, no tickets, no estimates. Work arrives as a **phase**: a YAML
spec with a goal, steps, and a definition of done. It gets written before any
code exists, validated against `phase-spec.schema.json`, and left alone once it
is complete. When scope changes, a successor appears (`p0169b`, `p0169c`) and
the original stays as it was.

That last part matters more than it looks. A spec you can edit after the fact
tells you what you wish you had decided.

Phases landed at about 3.4 a day and commits at 9.9, and the rate held across
the whole six months rather than spiking: 24 phases in March, 83 in May, 72 in
July. Alongside them, 185 releases, roughly seven a week, all automated.

## Steering the model

Eight layers do the steering. The top ones are text that the model can choose to
follow. The bottom ones are gates that stop it when it does not. Most of the
useful work happens at the bottom.

| Layer | What it is |
|---|---|
| `CLAUDE.md` | the read order for context files, the ten step workflow, the rules that never bend |
| `.agentsmith/contexts/*/coding-principles.md` | 245 lines of quality rules: max 20 lines per method, max 120 per class, one type per file |
| `.agentsmith/phases/{planned,active,done}/` | the backlog as a state machine, 679 schema validated specs |
| `.agentsmith/decisions/p{NNNN}.yaml` | one file per phase, every entry naming what was chosen, what it beat, and why |
| `.agentsmith/memory/` | 33 ratified behavioural rules, each traceable to a real correction |
| spec-first plugin | the workflow as a callable tool instead of a document |
| `.claude/hooks/phase-gate.sh` | **the blocking commit gate** |
| `hooks/pre-commit` | a gate that sits outside the agent entirely |

The gate is the one that carries the weight. A hook intercepts every
`git commit` whose message names a phase, and refuses it unless four checks
pass: the build, all 3,760 tests, four CLI dry runs, and every harness preset
without a crash.

CI would tell you about a break afterwards. The hook stops the commit from
happening. Whatever the model believes about its own work, it cannot put that
belief into the history by itself.

## Principles as tests

The strongest lever here is also the least conspicuous one. Every coding
principle that actually mattered got translated into an executable test under
`tests/AgentSmith.Tests/Architecture/`. Each of those tests has a cause behind
it, some concrete failure that had already happened once.

| Rule | What it enforces | What caused it |
|---|---|---|
| `FileLengthRatchet` | max 120 lines per file | 187 files already exceeded it, so they were ratcheted rather than exempted |
| `NoStaticState` | no static state in Application or Infrastructure | a static field is a dependency the composition root cannot see |
| `NonDiCtor` | function classes are constructible only through DI | a manual `new` bypasses every setting the operator configured |
| `ChatClientCallScope` | every model call sits inside a call scope | without it the cost event carries no role, phase or repository |
| `ModelOutputParsing` | check a value's *kind* before reading it | one run died on a single line, and nineteen more sites were waiting |
| `OneDeliveryGate` | exactly one method decides whether a run delivered | with two verdicts, a wrong verdict has two possible authors |
| `GateOrder` | a gate runs *after* the thing it judges | finished work got reported as undelivered, on every run |
| `StartupThrowInventory` | every startup path throw is accounted for | a ruling is not a mechanism, and the next throw appeared immediately |
| `DependencyPinning` | no fallback behind a guard | `--frozen-lockfile \|\| pnpm install` guarantees the guard can never fire |
| `PhaseRecord` | the phase record matches what the repository did | nine shipped phases were still sitting in `active/` |

### The ratchet

Suppose a rule is right and 187 files break it. Weakening the rule is bad, and
touching 187 files in one go is worse. The way out is a **ratchet**: freeze the
existing violations into a baseline file, then allow entries to leave that list
and never join it. Debt moves in one direction and nobody has to launch a
cleanup initiative to make it happen.

Two baselines run today, at 177 and 87 entries.

### Why this matters more with a model

Someone who breaks the same principle ten times a day will eventually learn it.
A model starts every session with no scars at all. So the safety has to live in
the API and the type system, because a warning comment does not survive contact
with ten commits a day. A failing test does.

## How do you know it is not lying?

Ask a model whether it delivered and it has every incentive to say yes. Faking
green is the central failure mode of agentic systems, and the way out is to stop
asking the model that question.

- **One verdict, one author.** Exactly one method in the system returns a
  `DeliveryVerdict`. An architecture test hunts for a second decider by return
  type instead of by name, and fails the moment one shows up.
- **The diff is what gets checked.** A run's outcome gets verified against the
  diff that was actually committed, never against the model's own summary.
- **The gate runs after the delivery.** Enforced as a test now, because for
  months it ran before.

All three came out of failures rather than foresight. One merge step once
promoted nine false findings to CRITICAL even though the responsible master had
dismissed them, because it read silence as absence of coverage.

## What did not work

A page that only lists successes reads as marketing and gets discounted whole.
So here are the dead ends.

**95 skills, then 12.** The skill catalog holds the role definitions that tell
the model what an architect or a reviewer does inside a run. It grew for five
months to a peak of 95 and then dropped to 12 in a single breaking release. 109
skill directories were created over the project's life and 95 were deleted. The
cliff in the git history marks the deletion; most of those skills had been out
of use for weeks before anything actually removed them.

The reason is worth more than the number. Earlier models needed tight direction,
so writing one precise role per sub task was the right call at the time. Stronger
models inverted it. Narrow toolsets and fine grained roles produced worse output
than a model with `bash`, read access and a clear goal. Four reviewer skills
collapsed into one master. Scaffolding around a model ages faster than the code
it writes.

**The plan generator** was retired after evaluations caught the model truncating
every multi repository plan at its output limit. The spec is the plan now.

**The context compactors** sat dead in the codebase for months and were deleted
outright once hysteresis based compaction arrived.

**The verb whitelist in the verification gate** checked the plan instead of the
contract and failed a flawless migration.

**A metric that read zero.** The most expensive mistake in the whole project was
not a crash at all. The cached token counter on OpenAI read a key the SDK no
longer filled, across four call sites, for months. The caching worked fine; only
the readout was wrong. That turned into a rule of its own: a stale declaration
is a premise that other decisions rest on, so correcting it reopens every one of
them.

**69 planned phases** are still open, next to a `PARKED.md` of architecture
problems that outlived the slice that found them. A visible backlog beats a
quiet shortcut.

## What was learned

33 behavioural rules live in `.agentsmith/memory/`, each traceable to a concrete
correction. Ten of them transfer beyond this repository.

1. **Challenge the premise before building.** At speed, the foundational
   assumptions in a spec go unexamined. "Is this actually deployed and working?"
   belongs before the implementation.
2. **When the fix is a heuristic around a missing field, the field is the fix.**
   Models bridge missing data plausibly, which hides the silent producer that
   never supplied it.
3. **No wrapper or adapter shims.** The convenient path is a bridge class that
   keeps the old interface alive, and the migration then never finishes.
   Migrate the callers directly.
4. **Split, don't defer.** Slicing a phase is healthy. Pushing load bearing
   wiring to "later" destroys testability.
5. **Unit tests with mocks bypass the composition root.** Composition bugs only
   surface through the real service provider and the real config loader.
6. **Verify it yourself.** Reproduce in the test harness before reporting
   anything. The person is there to decide, not to be your execution
   environment.
7. **Describe capabilities, not prohibitions.** Say what the agent can do and
   then stop. Narrow toolboxes demonstrably produce worse output.
8. **"It used to work" means read the history.** A chain of unrelated looking
   breakages calls for `git log -p` before any symptom chasing.
9. **Safety belongs in the type, not the discipline.** Warning comments do not
   scale with the speed.
10. **What the model learns has to be ratified.** A proposed rule stays a
    proposal until a human agrees, otherwise the system writes its own
    permissions.

## What stays with the human

"Autonomous" is the wrong word for any of this. The model writes specs, code,
tests and decision records. Four things sit with the person, and they happen to
be the four where a mistake gets expensive.

- **Ratification.** A new behavioural rule stays a proposal until a human
  agrees. A system that confirms its own rules has no rules.
- **Premises.** A model checks whether it completed the task. Whether it was the
  right task is a different question, and nobody else is going to ask it.
- **Design over repair.** Before any fix: does the surrounding behaviour make
  sense at all? A model will happily deliver a clean fix for behaviour that
  should never have existed.
- **The running system.** Containers and deployments never get touched
  unilaterally. The model diagnoses and proposes, and whoever carries
  operational responsibility runs the command.

The product mirrors this. A run that reaches a decision it should not make alone
will stop and ask, in the ticket or the dashboard, and resume on your answer
without burning tokens while it waits.

## The point

The `coding-principles.md` this project was built under is the same file that
Agent Smith injects into its own agents at runtime, through
`LoadCodingPrinciplesCommand`.

Everything that grew here as steering machinery, the spec before the code, the
decisions recorded with their rejected alternative, the principles as tests, the
gate the author cannot open, is the product. `init-project` teaches a foreign
repository the same structure this one was written under.

Which makes the claim narrower and more useful than "AI writes code now". The
model did not get steered better. The environment got built so that bad work
does not make it through, and that environment travels to any repository, with
or without Agent Smith. See [Methodology](methodology.md) for how it runs inside
a pipeline, and [Decision logging](../reference/concepts/decisions.md) for the
record format.

### Three things you can do without installing anything

1. **A `CLAUDE.md` at the repository root** that states which files to read in
   which order and what never bends. About an hour of work, and it separates a
   model that guesses from one that knows the state of play.
2. **One blocking commit hook.** Build and tests green, or no commit.
3. **A decision log that records the rejected option**, in `chose` / `over` /
   `reason` form. Recording only the chosen option is close to worthless. A
   decision becomes reconstructable through whatever stood beside it.

## How this was counted

Anyone should be able to reproduce these figures from a clone.

- **Period.** 18 Feb to 19 Aug 2026, 182 calendar days. February and August are
  partial months.
- **Commits.** `git rev-list --count HEAD` gives 1,799. Excluding bot commits,
  1,664 come from a single human author.
- **Code.** Every `.cs` file under `src/` and `tests/`, excluding `bin/` and
  `obj/`. Generated EF migrations are included.
- **Tests.** The count of `[Fact]` and `[Theory]` attributes, 3,662 plus 98. The
  number of executed cases runs higher, because Theory data rows expand.
- **Decisions.** Entries across the 483 YAML files in `.agentsmith/decisions/`.
- **Phases.** Files in `.agentsmith/phases/done/` (610) and `planned/` (69).
- **Releases.** Version headings in `CHANGELOG.md`.
- **Skills.** The number of `SKILL.md` files per commit in the
  `agent-smith-skills` catalog repository.
- **Working time.** Reconstructed from the timestamps of the 1,664 human
  commits. Commits less than 90 minutes apart count as one session, and each
  session gets 20 minutes of lead in. That yields 430 h across 255 sessions. A
  60/15 minute threshold gives 344 h, a 120/30 minute one gives 519 h. The
  figure quoted above is 500 h, deliberately at the upper middle of that band,
  because thinking time that produces no commit stays invisible to this method.

Runtime and success rates of production runs are missing, for lack of reliable
data. Token spend on the project's own development sits around €1,000 over six
months, as an order of magnitude rather than an audited figure.
