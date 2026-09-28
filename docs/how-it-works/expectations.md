# Expectations & durable dialogue

Two capabilities that belong together: the run writes down *what counts as done* before it writes code, and it can wait for your answer for days without holding a single pod.

## The done-list is the expectation

Every `code` run works from a specification. `DeriveSpec` reads the ticket after the code has been analyzed and cuts it into an ordered set of phases; each phase carries a goal, its steps and a "done when" list. That list is the run's acceptance contract. It goes into the master's prompt, it renders into the pull request as a reviewer checklist, and it is what the [delivery account](delivery-account.md) judges the branch against, criterion by criterion. When you review the PR you review it against those sentences, not against your memory of what the ticket kind of meant.

The cut is posted to the ticket as "this is how I understood the ticket": per phase the done-when criteria, the facts it established (each from a look it took at the code, with what it looked at), and the assumptions it made without one — the ones to correct if they are wrong. The run doesn't wait for you to ratify it. Blocking on a person who isn't there is what this replaces; a correction is the way back in.

How you correct it depends on where the specification came from.

**A cut the run derived.** Comment on the ticket, or edit the ticket text, and the next run re-cuts the phases that haven't started yet. Phases that already ran are kept. You can also edit a phase spec directly on the ticket branch under `.agentsmith/specs/`; the next run treats your commit as a correction and builds on it.

**A specification a person approved.** A ticket filed from an approved design conversation — in the dashboard's [Work it out](work-it-out.md) page or in chat — carries the `phase-spec:approved` label and its set is already on the ticket branch ([Spec dialogue](spec-dialogue.md) covers that side). The run works that set and never re-cuts it on its own: a comment or a ticket edit is reported once on the ticket as not acted on. Edit a phase that hasn't started on the branch and the next run works your edit; a phase that already ran is never edited, so a correction to one becomes a new phase. If you can't reach the branch at all, write a comment whose first line says nothing but `cut this specification again`, and the next run cuts the unstarted phases again from the ticket. If the approved set is not on the branch when the run starts, the run parks the ticket and says which path it looked at.

### Declined criteria

Sometimes a criterion asks for something no work in this repository can make true. The master can decline it, with what not doing it means. Declined criteria are never silently dropped: they show in the completion comment on the ticket, in the PR body under "Declined by the run", in `result.md`, and in the run's Verify card. The delivery account still judges the branch on its own.

## When the run hands the ticket back

Some tickets shouldn't be built as written, and the run says so instead of guessing. These hand-backs happen at derivation, before a single line is written; each one comments on the ticket and parks it.

- **The ticket reads two ways.** The comment lists both readings and names the one the run would take. Reply with the one you mean and move the ticket back to a trigger status. If nobody answers and the ticket comes back anyway, the next run proceeds on the reading it named and says so.
- **The requirement contradicts the repository.** Reply with what changes the picture and move the ticket back. A second contradiction with no reply in between ends the run as a failure rather than parking forever.
- **Not implementable as specified.** A verdict, not a question: it parks in your `not_implementable_status` (or `needs_clarification_status` when you have none), and a comment doesn't restart it. Change the ticket and use Retry on the run.

A ticket that asks for something that must not be done is refused even earlier, by the scope call; see [Lifecycle](lifecycle.md#in-seven-steps).

## Durable dialogue: checkpoint at the ask, resume on the answer

When the master needs a person mid-run, asking doesn't mean a process waiting on stdin. It means a checkpoint:

1. The run posts its question on the ticket, moves the ticket to `needs_clarification_status`, labels it `agent-smith:waiting`, and mentions the assignee (or the reporter when nobody is assigned, or says plainly that nobody was notified). With `dialogue.dashboard_url` set, the comment links to the run's page in the dashboard.
2. The run serializes its pipeline context, releases its lease and its compute, and gets the status **`waiting_for_input`**. Pods gone, cost stopped. A mid-run question has no deadline.
3. You answer. In the dashboard the question card has a **Send & resume run** button. On Azure DevOps, a comment on the ticket works too: the first comment after the question by anyone but Agent Smith is taken as the answer.
4. A background sweeper (leader-elected, every 15 seconds) picks up answered checkpoints. The run re-enters at the asking step with your answer handed to the master, under the same run id. It queues through the normal capacity queue like any other run; nothing jumps the line.

On GitHub, GitLab and Jira, answering on the ticket and moving it back to a trigger status starts a fresh run, which reads your reply as part of the ticket.

The practical consequence: asking the human stops being expensive. A run that would rather ask than guess doesn't block a sandbox slot for two days, so the system can afford to ask whenever the ticket is genuinely ambiguous.

## What you see

- Runs list: `waiting_for_input` runs are visibly parked; a resumed run waiting for capacity shows like any queued run, with its position.
- The ticket: the question or hand-back as a comment, the ticket parked in `needs_clarification_status` where that applies.
- `result.md`: the delivery account, declined criteria, and any ticket instructions the run chose to ignore.

## Next

- [The delivery account](delivery-account.md) — how the done-list is judged.
- [Spec dialogue](spec-dialogue.md) — the conversational front door.
- [Lifecycle](lifecycle.md) — where these steps sit in the run.
- [Capacity & queueing](../reference/operations/capacity.md) — the queue a resumed run rides.
