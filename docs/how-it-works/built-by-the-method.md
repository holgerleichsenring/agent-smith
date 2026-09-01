# Built by the method it teaches

I built this thing because I got tired of AI that hands you a confident wrong
answer, dressed up better each time. The way out was a paper trail: every change
tied to evidence, every choice recorded next to the one it beat. That is what
[Methodology](methodology.md) describes.

This page is the other half. If the paper trail is any good, it should be able to
account for the repository that produced it, so: what got built, over what time,
with which machinery holding it together. Every number here comes out of the repo
and the counting method is at the bottom, so you can check me.

The short version is that Agent Smith bootstraps an `.agentsmith/` directory into
your repo, with a context file, phase specs, a decision log and a memory of what
it learned. That is the product. It is also how this repository got written, over
six months, by me and one model.

## The numbers

| | |
|---|---|
| **610** | completed phases, each specified before a line of code existed |
| **2,587** | recorded decisions, each naming the alternative it beat |
| **244,753** | lines of C# across 3,044 files in 31 projects |
| **3,760** | automated tests, gating every single commit |
| **1,799** | commits, 350 merged PRs, 185 releases |
| **~500 h** | of human time, roughly 50 minutes per completed phase |

That covers 18 February to 19 August 2026, so 182 calendar days. One human, one
model.

Two ratios tell you more than the totals do. There are 245 lines of specification
for every 1,000 lines of code, which makes the spec about a quarter of everything
written. And there is 0.65 of a line of test code for every line of production
code. That second one is deliberate, and the section on architecture tests below
is where it pays off.

## The unit of work is the phase

Work here arrives as a **phase**. It is a YAML file with a goal, some steps and a
definition of done, and I write it before any code exists. It gets validated
against `phase-spec.schema.json`, and once it is complete I leave it alone. If
the scope moves, a successor shows up (`p0169b`, then `p0169c`) and the original
stays exactly as it was.

That last bit matters more than it looks. A spec you can edit afterwards tells
you what you wish you had decided, which is a pretty useless thing to read six
months later.

Phases landed at about 3.4 a day and commits at 9.9, and the rate held across the
whole six months instead of spiking. 24 phases in March, 83 in May, 72 in July.
Alongside them 185 releases, roughly seven a week, all automated.

## Steering the model

Eight layers do the steering. The top ones are text the model can choose to
follow, and the bottom ones are gates it crashes into when it does not. The
gates do the heavy lifting.

| Layer | What it is |
|---|---|
| `CLAUDE.md` | the read order for context files, the ten step workflow, the rules that always hold |
| `.agentsmith/contexts/*/principles.md` | 245 lines of quality rules: max 20 lines per method, max 120 per class, one type per file |
| `.agentsmith/phases/{planned,active,done}/` | the backlog as a state machine, 679 schema validated specs |
| `.agentsmith/decisions/p{NNNN}.yaml` | one file per phase, every entry naming what got chosen, what it beat, and why |
| `.agentsmith/memory/` | 33 ratified behavioural rules, each traceable to a real correction |
| spec-first plugin | the workflow as a callable tool, so it gets invoked rather than interpreted |
| `.claude/hooks/phase-gate.sh` | the blocking commit gate |
| `hooks/pre-commit` | a gate that lives outside the agent entirely |

A hook intercepts every `git commit` whose message names a phase, and it lets the commit through once four things come back
green: the build, all 3,760 tests, four CLI dry runs, and every harness preset
running crash free.

CI would have told me about a break afterwards. The hook stops the commit from
existing. Whatever the model believes about its own work, that belief has to get
past a build before it reaches the history.

A hook can only read the commit it is shown, so a few invocation forms carry no
message it can inspect and are let through — loudly, and with a line in the gate's
ledger saying so. `CLAUDE.md` lists exactly which, because a definition of done that
leans on the gate has to know where the gate stops.

## Principles as tests

This is the part that does the steering, and the reason the test ratio above
looks the way it does. Every coding principle that mattered got turned into an
executable test under
`tests/AgentSmith.Tests/Architecture/`, and every one of those tests has a story
behind it, some concrete thing that had already gone wrong once.

| Rule | What it enforces | What caused it |
|---|---|---|
| `FileLengthRatchet` | max 120 lines per file | 187 files were already over, so they got ratcheted instead of a pass |
| `NoStaticState` | Application and Infrastructure hold their state in DI | a static field is a dependency the composition root cannot see |
| `NonDiCtor` | function classes are constructible only through DI | a manual `new` bypasses every setting the operator configured |
| `ChatClientCallScope` | every model call sits inside a call scope | outside one, the cost event loses its role, phase and repository |
| `ModelOutputParsing` | check a value's *kind* before you read it | one run died on a single line, and nineteen more sites were waiting |
| `OneDeliveryGate` | exactly one method decides whether a run delivered | with two verdicts, a wrong verdict has two possible authors |
| `GateOrder` | a gate runs *after* the thing it judges | finished work got reported as undelivered, on every single run |
| `StartupThrowInventory` | every startup path throw is accounted for | p0391a ruled on it, and p0393 found the next one in a file p0391a had already probed |
| `DependencyPinning` | a guard fires | `--frozen-lockfile \|\| pnpm install` guarantees it never can |
| `PhaseRecord` | the phase record matches what the repository did | nine shipped phases were still sitting in `active/` |

### The ratchet

Here is the situation. A rule is right, and 187 files break it. Weakening the
rule is bad, and touching 187 files in one go is a change nobody can review.

So the limit stays non negotiable and the 187 files get a **ratchet** instead of
an exemption. The baseline records each file's length at the moment the rule went
in. From there a listed file may only get shorter, no new file may join the list,
and a file that drops under the limit has to leave it. The debt moves one
direction, and it does that without anybody scheduling a cleanup week. Two
baselines run today, at 177 and 87 entries.

### Why this matters more with a model

You break the same principle ten times a day and eventually you learn it. A model
walks into every session with a clean slate and no scars, which is exactly what
makes it fast and exactly what makes it repeat itself. So the safety has to sit
in the API and the type system. A warning comment relies on the next author
reading it. A failing test does not.

## How do you know it is not lying?

Ask a model whether it delivered and it has every incentive in the world to say
yes. Faking green is the big failure mode of
agentic systems, and my answer is to keep that question away from the model
entirely.

Exactly one method in the whole system returns a `DeliveryVerdict`. An
architecture test hunts for a second decider by return type instead of by name,
so a new one fails the build the moment it exists. The outcome itself gets
verified against the diff that actually landed in the branch, and the model's own
summary of what it did never enters into it. And the gate runs after the
delivery, which sounds obvious until you find out it ran the other way round for
months.

All three of those came out of things breaking rather than me being clever up
front. One merge step promoted nine false findings to CRITICAL even though the
master responsible had already dismissed them, because it read silence as
absence of coverage. That kind of thing is where the rules come from.

## What did not work

A page that only lists the wins reads like marketing and you would be right to
discount it. So here are the dead ends.

**95 skills, then 12.** The skill catalog holds the role definitions that tell
the model what an architect or a reviewer does inside a run. Its first commit in
late April already held 42, it reached 95 by the end of July, and the next day it
dropped to 12 in one breaking release. 109 skill directories got created over the
project's life and 95 got deleted. The cliff in
the git history is the deletion, though most of those had been sitting unused for
weeks before anything removed them.

The reason is worth more than the number. Earlier models needed tight direction,
so writing one precise role per sub task was genuinely the right call back then.
Stronger models flipped it. Narrow toolsets and fine grained roles gave me worse
output than a model with `bash`, read access and a clear goal. Four reviewer
skills collapsed into one master. Scaffolding around a model ages faster than the
code it writes.

**The plan generator** got retired after evaluations caught the model truncating
every multi repository plan at its output limit. The spec is the plan now.

**The context compactors** sat dead in the codebase for months and got deleted
outright once hysteresis based compaction arrived.

**The verb whitelist in the verification gate** checked the plan instead of the
contract and failed a perfectly good migration.

**A metric that read zero.** The cached token counter on OpenAI was reading a key
the SDK had stopped filling, across four call sites, for months. Nothing broke,
which is exactly why it lasted that long: the caching underneath worked the whole
time and only the readout was wrong, so there was no failure to notice. It turned
into a rule of its own. A stale declaration is a premise other decisions rest on,
so fixing it reopens every one of them.

**69 planned phases** are still open, sitting next to a `PARKED.md` of
architecture problems that outlived the slice that found them. One of the 33
memory rules covers this: slice work openly and point at the named follow up,
rather than letting scope disappear quietly.

## What was learned

33 behavioural rules live in `.agentsmith/memory/`, each one traceable back to a
correction I actually had to make. Ten of them travel beyond this repository.

1. Challenge the premise before building. At speed, the foundational assumptions
   in a spec go unexamined, and "is this actually deployed and working?" belongs
   before the implementation rather than after it.
2. When the fix is a heuristic around a missing field, the field is the fix.
   Models bridge missing data plausibly, and that is exactly what hides the
   silent producer that never supplied it.
3. Skip the wrapper and adapter shims. The convenient path is a bridge class that
   keeps the old interface alive, and then the migration never finishes. Migrate
   the callers directly.
4. Split, and finish each slice. Cutting a phase into pieces is healthy. Pushing
   load bearing wiring to "later" destroys testability for whoever has to look at
   it next, which is usually me.
5. Unit tests with mocks bypass the composition root. Composition bugs surface
   only through the real service provider and the real config loader.
6. Verify it yourself. Reproduce in the test harness before reporting anything.
   The person on the other side is there to decide, and using them as your
   execution environment wastes both of you.
7. Describe capabilities. Say what the agent can do, then stop. Narrow toolboxes
   demonstrably produce worse output than a broad one plus a clear goal.
8. "It used to work" means read the history. A chain of unrelated looking
   breakages calls for `git log -p` before you chase a single symptom.
9. Safety belongs in the type. Warning comments and careful operators hold up
   fine at human speed and fall apart at this one.
10. What the model learns has to be ratified. A proposed rule stays a proposal
    until I agree to it, otherwise the system quietly writes its own permissions.

## What stays with me

I would avoid the word autonomous here. The model writes specs, code, tests and
decision records, which is most of the volume. Four things stay on my side, and
they turn out to be the four where a mistake gets expensive.

Ratification is the first. A new behavioural rule sits marked as a proposal until
I agree to it, and all 33 memory entries carry that status. A system that
confirms its own rules has no rules.

Premises are the second. A model checks whether it completed the task. Whether it
was the right task is a different question, and nothing in the loop asks it.

Then design, ahead of repair. Before any fix comes the question of whether the
surrounding behaviour makes sense at all. A model will hand you a clean, tested
fix for behaviour that should never have existed, and it looks great in review.

And the running system stays mine. Containers and deployments never get touched
unilaterally. The model diagnoses freely and proposes, and whoever carries
operational responsibility types the command.

The product works the same way. A run that hits a decision it should not make
alone will stop and ask you, in the ticket or the dashboard, and pick up again
on your answer without burning tokens while it waits.

## The point

The `principles.md` I built this project under is the same file Agent
Smith injects into its own agents at runtime, through
`LoadCodingPrinciplesCommand`.

Everything that grew here as steering machinery is the product. The spec before
the code, the decisions recorded with the option they beat, the principles as
tests, the gate I cannot open myself. `init-project` teaches a foreign repository
the same structure this one was written under, so you get the setup rather than
the story about it.

Which makes my claim smaller and more useful than "AI writes code now". I stopped
trying to steer the model better and built an environment where bad work gets
caught on the way out. That environment travels to any repository, with or
without Agent Smith. Have a look at [Methodology](methodology.md) for how it runs
inside a pipeline, and [Decision logging](../reference/concepts/decisions.md) for
the record format.

### Three things you can do without installing anything

Put a `CLAUDE.md` at your repository root that says which files to read in which
order and what always holds. About an hour of work, and it separates a model that
guesses from one that knows where the project actually stands.

Add one blocking commit hook. Build and tests green, or the commit does not
happen.

And keep a decision log that records the option you rejected, in `chose` / `over`
/ `reason` form. Writing down only what you picked is close to worthless. The
decision becomes reconstructable through whatever was standing next to it, and
that is the part you will want in six months.

## How this was counted

You should be able to reproduce all of this from a clone.

- **Period.** 18 Feb to 19 Aug 2026, 182 calendar days. February and August are
  partial months.
- **Commits.** `git rev-list --count HEAD` gives 1,799. Take out the bot commits
  and 1,664 come from a single human author.
- **Code.** Every `.cs` file under `src/` and `tests/`, leaving out `bin/` and
  `obj/`. Generated EF migrations are in there.
- **Tests.** The count of `[Fact]` and `[Theory]` attributes, 3,662 plus 98. The
  number of executed cases runs higher, because Theory data rows expand.
- **Decisions.** Entries across the 483 YAML files in `.agentsmith/decisions/`.
- **Phases.** Files in `.agentsmith/phases/done/` (610) and `planned/` (69).
- **Releases.** Version headings in `CHANGELOG.md`.
- **Skills.** The number of `SKILL.md` files per commit in the
  `agent-smith-skills` catalog repository.
- **Working time.** Reconstructed from the timestamps of those 1,664 human
  commits. Commits less than 90 minutes apart count as one session, and each
  session gets 20 minutes of lead in. That gives 430 h across 255 sessions. A
  60/15 minute threshold gives 344 h and a 120/30 minute one gives 519 h. The
  figure I quote above is 500 h, sitting at the upper middle of that band,
  because thinking time that produces no commit stays invisible to this method
  and there was plenty of it.

Runtime and success rates for production runs are missing, because I have no
reliable data on them yet. Token spend on the project's own development sits
around €1,000 over six months, which is an order of magnitude rather than an
audited figure.
