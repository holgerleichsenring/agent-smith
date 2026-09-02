# What the adversarial review changed, 2026-09-01

Six phases were drafted against the five open questions on the two scans. Three
independent reviewers read them against the code and the pinned SDK. Two were
withdrawn before a line was written and two more were re-argued. What follows is
what the review falsified, so the same ground is not walked twice.

## Withdrawn

**2026-09-01-341b, "the master answers in its own words".** Its premise was that a
narrated finding is discarded. It is not: `TolerantJsonParser.ExtractSpan` already
takes the first `[` to the last `]` after fence-stripping, so prose around an array
parses today, and an unreadable answer is already recorded as `ScanTriageDegraded`
by 2026-08-30-03e4. What is actually broken is one gate and one budget, which
2026-09-01-6c32 fixes without a second model call. A reading turn that renders an
array from prose it did not itself read is also a citation-invention surface: the
`MasterReadPaths` anchor checks WHICH FILE, never which line, so a reader quoting a
file the master merely named passes the anchor with a line it invented. That is the
input 2026-09-01-85b2 would then treat as evidence. Revisit only if 6c32 measurably
fails to recover the answers.

**2026-09-01-acbd, "a scan is scored against a known target".** It silently
re-specified `2026-08-28-cc40`, which is committed, planned and unbuilt, and merged
in cc40's own named successor as well. cc40 is better on the two decisions that
matter: it scores per FILE (a correct detection that cites the call rather than the
sink still counts), and it counts an undeclared finding as a false alarm over a
clean-trap denominator — acbd's "extra is reported, not punished" cannot see the nine
false-positive criticals p0429 measured. cc40 is built as written; the api half
becomes 2026-09-01-6686, which is what cc40 promised in prose.

## Falsified premises worth keeping written down

- **The SDK already answers at the ceiling.** `Microsoft.Extensions.AI` is pinned at
  10.3.0; `FunctionInvokingChatClient` calls `PrepareOptionsForLastIteration` when
  `iteration >= MaximumIterationsPerRequest`, which strips every tool declaration, and
  then makes one more call. A closing turn does not need building.
- **Tool calls are not iterations.** One iteration executes every function call in a
  response, in parallel where allowed. 97 tool calls over three runs says nothing about
  how many iterations were used, and nothing in the repo records iterations.
- **The scan masters are not 7.5k.** They carry `{{ref:}}` tokens that
  `SkillBodyResolver` inlines: security-master is 7,574 bytes on disk and about 12,636
  after inlining. Both numbers previously quoted in this discussion were wrong.
- **The refuter already sends one call for all candidates.** Batching raises the
  round-trip count; the reason to batch is prompt size, not call count.
- **A finding whose cited file cannot be read is dropped, not passed through.**
  `FindingSubstantiator` removes `CandidateSet.Unresolvable` from delivery. Widening
  the checked set without changing that fate deletes master findings on a path the
  single-sandbox evidence reader cannot resolve.
- **`result.md` already prints `turns:`, and it is always 0.**
  `LimitEnforcer.RecordLlmCall` has no production caller.

## The two structural differences nobody had specced

Both came out of comparing the pipeline with the direct probe that found what twelve
pipeline runs did not.

1. **The master is shown the scanners' findings before it sees any code.**
   `ScanMasterPromptFactory.BuildFindingsSection` renders every raw observation into
   the review prompt, and the closing line asks the master to "work your methodology
   over these scanner inputs and the source". The probe had no such list. This is
   2026-09-01-0e80.
2. **The master may not run anything.** The scan prompt forbids builds and tests; the
   probe could write a scratch file and execute it. Left unspecced deliberately — it is
   a real change to a read-only guarantee and belongs to the operator, not to this batch.

## Four scored runs, and what they actually compare

The first two runs were reported here as a variance measurement of an unchanged scan.
That was wrong, and the correction matters more than the original claim.

The first run happened inside the scoreboard worktree, which was cut from the spec commit
and carries only cc40 and 6686 — none of the six phases that change how the scan behaves.
The second ran on the merged branch. So those two were a BEFORE and AFTER of the batch,
not two samples of one configuration. Two further runs were then taken on the merged
branch, giving three samples of the shipped behaviour.

| security corpus | before the batch | merged, 3 runs |
|---|---|---|
| misses | 0/5 | 0/5, 0/5, 0/5 |
| false alarms | 1/5 | 0/5, 0/5, 0/5 |
| cited line matched | 4/5 | 3/5, 4/5, 3/5 |

| api corpus | before the batch | merged, 3 runs |
|---|---|---|
| misses | 0/4 | 0/4, 0/4, 0/4 |
| false alarms | 0/3 | 0/3, 0/3, 0/3 |

**The floor is already at the ceiling, and that is the finding.** Every declared weakness
was named in every run, before the batch as well as after, so these corpora cannot show
whether any of the six phases improved detection. They prove the scan is wired and reaches
the code — which twelve live runs could not establish — and they are saturated for the
question the batch was built to answer. The next corpus has to be hard enough to be
failed, and cc40's own header already says why a planted defect is not: it is formulaic in
a way a real defect is not.

The single false alarm that disappeared is one sample against three and is not a claim.

**Severity is the unstable axis, and it is unstable on identical code.** Across the three
merged runs, `GET /members/{id}` was scored Medium, High, High and `GET /orders` Medium,
High, Medium, while the other two endpoints held. SpawnFix fires on Critical/High and
escalation reads Critical/High, so the same defect earns a remediation PR on one run and
not on the next. A phase that makes severity a judgement against stated criteria rather
than a free field is what this measurement asks for next. It is not built here: nothing is
tuned before it is measured, and this is the first measurement.
