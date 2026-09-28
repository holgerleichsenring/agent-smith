# The delivery account

Every phase spec ends in a "done when" list. The delivery account is how a run decides whether the branch actually delivers it — one criterion at a time, against what the branch carries, by a reader that never saw how the work was done.

I built it this way because "the tests are green" and "the ticket is done" are two different claims. A master that wrote the code and then reports its own work as finished is one party grading itself. The account is the second opinion, and it is the only thing the run's verdict listens to.

## When it is taken

The account is the second half of `VerifyPhase`. The first half runs the verify stages each repository declares in its context — build, test, lint, whatever the repo wrote down ([the `verify` block](../reference/concepts/context-file.md#verify-is-what-proves-a-change)). A red stage wins: an account taken over a tree that doesn't build would be an opinion about work nobody can ship, so the phase fails on the stage and names the command, the exit code and the directory it ran in.

When the stages pass, the account is taken per repository, over the phase's ratified criteria. It is also taken first, when a phase is entered: a phase whose criteria the branch already satisfies is recorded as done and skipped.

A run where no repository ran any verify command over a change it delivered fails, and says what was searched per repository. Nobody checked what shipped, and that isn't a pass.

## What a criterion can be

Each row ends in one of four dispositions:

| Disposition | Shown as | Means |
|---|---|---|
| `met` | proven | The branch delivers it, and the row cites the evidence |
| `unmet` | failed | The branch does not show it |
| `not_applicable` | n/a | The criterion's condition does not hold in this repository, proven by a search of the base that ran and found nothing |
| `unproven` | unproven | The account could not be taken at all — a red build, a diff that would not run — and the row says why |

`unproven` and `unmet` are kept apart on purpose. A criterion nobody could measure and a criterion that failed are different facts, and you act on them differently.

## Evidence, and how it is checked

A `met` row has to cite something real. A criterion about the repository's content is satisfied by a path the delivery really touched; a criterion about a build or test result is satisfied by a command that really ran. A citation that resolves to neither is a fabrication and the criterion goes back to unmet.

The account doesn't have to take the agent's word for anything. It can search the branch itself, and the base ref of each repository too, and every search it reports names the ref it read. "Not there on `origin/main`" and "not there on the branch" are the two answers you need to be able to tell apart. Each accounting pass gets its own allowance of twelve searches, so an early pass can't spend the budget a later one needs.

A `not_applicable` row has to bring more than a claim. It must cite a search of a real base ref that ran and matched nothing, and name the condition it takes to be false. A row that can't do that is not an error; it falls back to unmet. An account where every row is `not_applicable` and nothing is `met` does not pass either — it names what went unjudged, so you can overrule one line rather than the whole verdict.

Two follow-up questions exist, each asked once:

- **The citation correction.** A row that claimed `met` but cited something that resolves to nothing is asked again, with the objection and the accepted citation form. A row that answered unmet is never re-asked: that's an answer, not a formatting problem.
- **The full-reach pass.** Large deliveries are read in diff windows. A criterion that no single window could settle — "every host in both repos configures X" — is put once more with the whole branch and its base in reach, the complete file list and a fresh search allowance. This pass can only raise a disposition, never lower one a window already settled, and the run says what it changed.

## What happens with the result

All criteria met: the phase is verified and the next one starts.

Something outstanding: the list goes back to the master for one repair pass — it writes, the work is committed, the phase is verified again. One pass, not a loop. If the criterion is still outstanding afterwards, the phase fails. What that means for the run is on [Lifecycle](lifecycle.md#when-it-goes-wrong): a failure if nothing verified before it, a shortfall if earlier phases did.

## Declined criteria

Sometimes a ticket asks for something no change inside this repository can make true. The master can decline such a criterion, with what not doing it means. Declined criteria are listed beside the account, never among its rows — the account still judges the branch on its own. You find them in the ticket comment, in the PR body under "Declined by the run", in `result.md`, and in the run's Verify card under "Declined by the agent".

## Where you read it

- The pull request body, under "What this run accounted for", itemised per repository.
- `result.md` in the run record, and the phase record committed to the branch.
- The run detail in the [dashboard](../reference/operations/dashboard.md): the **Verify against acceptance** beat shows the card "Verify against the ratified acceptance", one row per criterion with its disposition, its citation and the reason. The badge reads "N of M proven" or "N of M · K failed". A hint above the rows says whose read you are looking at — the independent account the gate decided on, or, on runs that had none, the agent's report on its own work.

## When the verdict is wrong

Next to each criterion there is a "this verdict is wrong" link. It opens a small form: pick what it actually was — "it was met", "it was not met", "it did not apply", "nothing measured it" — and write why. The reason is required. After you record it, the row shows "*your name* says: …" with the reason, and you can withdraw it later.

Recording a judgement changes nothing about the run. It doesn't re-open the gate, re-run the phase, move the run's state or touch the PR. It is a labelled record of where the account got it wrong, so the account can be measured against people who know better. If you want the work redone, re-trigger the ticket.

## Next

- [Lifecycle](lifecycle.md) — where verification sits in a run.
- [Expectations & durable dialogue](expectations.md) — where the "done when" list comes from.
- [The context file](../reference/concepts/context-file.md) — declaring the verify stages.
