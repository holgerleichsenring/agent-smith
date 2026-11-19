# Expectations & durable dialogue

Two capabilities that belong together: the run writes down *what counts as done* before it writes code, and it can wait for your answer for days without holding a single pod.

## The done-list is the expectation

Every `code` run works from a specification: the series on the ticket branch, or — for a ticket that has none — the spec its coding master writes first from the ticket and the code (see [the code pipeline](../reference/pipelines/fix-and-feature.md#a-ticket-without-a-spec)). Each spec carries a goal, its steps and a "done when" list. That list is the run's acceptance contract. It goes into the master's prompt, it renders into the pull request as a reviewer checklist, and it is what the master [reports on](delivery-account.md), line by line. When you review the PR you review it against those sentences, not against your memory of what the ticket kind of meant.

The spec is on the ticket branch and in a draft spec PR while the run works. The run doesn't wait for you to ratify it. Blocking on a person who isn't there is what this replaces; a correction is the way back in.

How you correct it depends on where the specification came from.

**A spec the run wrote.** Comment on the ticket, or edit the ticket text: the next run hands that input to the master of the next unstarted spec, which amends it. When every spec already ran, the input starts a new spec. Specs that already ran are kept.

**A specification a person approved.** A ticket filed from an approved design conversation — in the dashboard's [Work it out](work-it-out.md) page or in chat — carries the `spec:approved` label and its set is already on the ticket branch ([Spec dialogue](spec-dialogue.md) covers that side). The run works that set and never re-cuts it on its own: a comment or a ticket edit is reported once on the ticket as not acted on. To change a spec, say what should change in the ticket's conversation. Nobody edits the branch by hand. While a run is building a spec, a message from someone who may control runs reaches its agent, which can revise the spec it is building. The changed spec is checked against the spec schema and written to the ticket branch as the next revision of the series; no reviewer stands between the master and its own spec. A change to another spec is raised again once the run has ended. Otherwise the conversation proposes the change, and once you approve it, it is recorded for the next attempt, which continues the series by spec id. A spec that already ran is never edited, so a correction to one becomes a new spec. If you work from the tracker alone, write a comment whose first line says nothing but `cut this specification again`, and the next run cuts the unstarted specs again from the ticket. If the approved set is not on the branch when the run starts, the run parks the ticket and says which path it looked at.

## When the run hands the ticket back

Some tickets can't be worked as they stand, and the run says so instead of guessing; each case comments on the ticket and parks it. The first four are raised by series written before 2026-11-19, when a model cut the ticket; today the coding master asks on the ticket when only a person can answer.

- **The ticket reads two ways.** The comment lists both readings and names the one the run would take. Reply with the one you mean and move the ticket back to a trigger status. If nobody answers and the ticket comes back anyway, the next run proceeds on the reading it named and says so.
- **The requirement contradicts the repository.** Before the ticket parks, a contradiction that cites no evidence is asked again, and on the last attempt it parks; one that cites evidence parks the ticket: reply with what changes the picture and move the ticket back. A second contradiction with no reply in between ends the run as a failure rather than parking forever.
- **Not implementable as specified.** A verdict, not a question: it parks in your `not_implementable_status` (or `needs_clarification_status` when you have none), and a comment doesn't restart it. Answer it in the ticket's conversation on the dashboard: approving a change there starts the next attempt. Retry is not for this — it repeats an attempt unchanged, and only after an infrastructure end.
- **The work could not be divided between the repositories.** In a scope of several repositories, every spec has to name exactly one of them, and no cut did. Reply with the repository each part belongs to, or split the ticket per repository, and move it back to a trigger status; the next run cuts again.
- **The series is out of step between its repositories.** The repositories carry different revisions of the series, and the comment names the ones that lag. Nothing runs on a mix. Either write a comment that opens with `cut this specification again` on a line of its own and move the ticket back to a trigger status, which cuts the series again over every repository and keeps the specs that already ran, or approve the specification again in the design conversation and file the ticket again.
- **The approved specification is not on the ticket branch.** The ticket was filed from an approval, and the run can't read a series for it on the branch. The comment says whether nobody approved the ticket or the approval never reached the branch. Put the specification on the ticket branch and move the ticket back to a trigger status.

A ticket that asks for something that must not be done is refused even earlier, by the scope call; see [Lifecycle](lifecycle.md#in-seven-steps).

## Durable dialogue: checkpoint at the ask, resume on the answer

When the master needs a person mid-run, asking doesn't mean a process waiting on stdin. It means a checkpoint:

1. The run posts its question on the ticket, moves the ticket to `needs_clarification_status`, labels it `agent-smith:waiting`, and mentions the assignee (or the reporter when nobody is assigned, or says plainly that nobody was notified). With `dialogue.dashboard_url` set, the comment links to the run's page in the dashboard.
2. The run serializes its pipeline context, releases its lease and its compute, and gets the status **`waiting_for_input`**. Pods gone, cost stopped. A mid-run question has no deadline.
3. You answer. In the dashboard the question card has a **Send & resume run** button. On Azure DevOps, a comment on the ticket works too: the first comment after the question by anyone but codeably is taken as the answer.
4. A background sweeper (leader-elected, every 15 seconds) picks up answered checkpoints. The run re-enters at the asking step with your answer handed to the master, under the same run id. It queues through the normal capacity queue like any other run; nothing jumps the line.

On GitHub, GitLab and Jira, answering on the ticket and moving it back to a trigger status starts a fresh run, which reads your reply as part of the ticket. The same move works on a ticket whose last run finished: moving it back into a trigger status is read as the rework act, so the run keeps the specs already done and appends new ones (see [Rework a finished ticket](../trigger-it/webhooks.md#rework-a-finished-ticket)).

The practical consequence: asking the human stops being expensive. A run that would rather ask than guess doesn't block a sandbox slot for two days, so the system can afford to ask whenever the ticket is genuinely ambiguous.

## What you see

- Runs list: `waiting_for_input` runs are visibly parked; a resumed run waiting for capacity shows like any queued run, with its position.
- The ticket: the question or hand-back as a comment, the ticket parked in `needs_clarification_status` where that applies.
- `result.md`: the masters' reports and any ticket instructions the run chose to ignore.

## Next

- [How a spec is checked](delivery-account.md) — how the done-list is reported on.
- [Spec dialogue](spec-dialogue.md) — the conversational front door.
- [Lifecycle](lifecycle.md) — where these steps sit in the run.
- [Capacity & queueing](../reference/operations/capacity.md) — the queue a resumed run rides.
