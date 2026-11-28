# Series run

<p class="cb-lead">When you file two or more tickets from one Work it out conversation, a series run looks after all of them: it starts and retries tickets, reports in the conversation, takes your messages there, and checks the finished work on main.</p>

!!! note "Two kinds of series"
    A *ticket's* series is the set of specs one ticket builds, possibly over several attempts ([Lifecycle](lifecycle.md#a-series-over-several-attempts)). A *series run* is one level up: it follows the tickets of one filing. This page is about the second.

<figure class="cb-shot" markdown>
![A series in its Work it out conversation: the opening line, a status table and the Series tab](../assets/screenshots/series-status.png)
<figcaption>The series speaks in the conversation you filed from when you are needed; current versions post no status table there. The Series tab on the right shows progress, cost, what runs now, the critical path and the policy.</figcaption>
</figure>

## When it opens

The `file` run that files your tickets opens the series before it starts them. One ticket on its own is a series of one: its code run opens it when no series holds the ticket. Its master leads its pull requests, CI and merge like any series ticket, but its page stays where you look and answer: its questions, comments and merge stay on the ticket page, and it ends once the ticket merged, without a series check.

## How it works

The series is a record, not a long-running process. Its work happens in short **beats**, each a queued run with its own run id, so no run outlives the server's wall-time limit and any server replica can take the next beat. Only one beat runs at a time.

In a beat the series master reads the facts of every ticket (state, requires, specs, pull requests, CI, cost) from the server's records, and acts through a small set of tools:

| It can | What that means |
|---|---|
| start a ticket | a filed ticket whose prerequisites allow it; the act log says whether a run started (with its id), was queued, or was refused, and why. You can start a ticket no run worked yet yourself with **Start** in its graph node's details |
| retry a ticket | a failed attempt, once it has looked at why |
| tell a ticket | send a message to the ticket's running or next run |
| add a ticket | file a prerequisite or a split-off part into the series, unstarted, with its requirements; it starts it once they stand |
| re-link tickets | add or remove what a ticket of the series waits for; a link that would make a ticket wait on itself is refused |
| ask you | post a question in the conversation |
| stop the series | when going on makes no sense, with the reason |

### One state per ticket

Every surface reads one state per ticket, worked out once on the server. That covers the master's `series_status` and `read_ticket`, the **Series** tab, the series page's Overview and Tickets, and the series list. The state starts from the ticket's hold, the same one its ticket page and the ticket list show. A ticket's CI is its open pull requests' CI on their current heads. A pull request that is closed or merged doesn't count. A CI read on an older head counts as running from the moment the head moved. Each ticket has one kind:

| Kind | Meaning |
|---|---|
| merged | landed |
| now | its pull request is the series' next merge |
| needed | other tickets start once its pull request merges |
| question | a question or a plan change waits on a person |
| failed | a failure, red CI, a conflict or a stop that a person decides |
| running | a run is working on it: coding, queued, resuming, restarting, waiting for its model place, or bringing its branch up to date |
| pr | its pull request waits: CI running, or behind another merge |
| waiting | not started, or waiting on its prerequisites |

A wait that runs past `holds.escalate_after_minutes` keeps its kind, and its state says it is overdue. The tickets a run is working on are what the series is working on now (`workingOn`).

Tickets still start and merge by the project's own rules: [stacked start](stacked-start.md) for dependents, the [merge policy and CI loop](ci-loop.md) for merging. The series doesn't watch CI itself; it reads what the CI loop recorded.

The master's own spend has a budget, `dialogue.series_master_budget_usd` (default 20 USD). The ticket runs keep their own caps. Once no more than `dialogue.series_master_budget_ask_usd` (default 2 USD) is left, the series asks once in the conversation: raise the budget by `dialogue.series_master_budget_usd`, or stop the series. Raising is the recommended choice, but the question has no deadline, so nothing is raised or stopped until a person answers with one of the choices (a button, or just "raise" or "stop"). Any other answer goes to the master, and the question comes back in a later beat. A raise adds to the setting, so changing the setting later still counts. While the budget question waits, a ticket's question waits behind it and is passed on once it is answered.

Events on the series' tickets wake the master: a pull request's CI reaching a verdict on its head (green, red, not started), a pull request that merged, closed, conflicts or moved to another target, a ticket run's end, and every comment on a ticket or its pull request, with or without the keyword. On a ticket of a filed series, a comment doesn't start a rework. The master gets it, quoted with its author, and decides: it can pass the comment on to the ticket or retry it. On a pull request, and on a GitHub or GitLab issue, a comment reaches the master only when its author can write to the repository; a stranger's comment on a series ticket starts nothing. Jira and Azure DevOps comments come from the organisation's own members and are passed on as they are. The master reads a comment as data from its author, not as the operator's words or as instructions. A paused series keeps its events and hands them over at the first beat after resume. A standalone ticket (a series of one) keeps its rework.

## What you see in the conversation

The series posts an opening line saying what it filed, what started, what waits and how it will merge. After that the conversation holds only what a person reads: a question and the answer passed on, what the master did (a retry, a rework, a pause), its reply when you write to it, and the series check's summary. It also gets one sentence when tickets merge ("#4 and #7 merged; 9 of 25 are in."), when one gets stuck and waits for your decision, when the final pull request opens and when the series is done. It posts no status: the series page shows every ticket's state and what waits on you.

On the series page, one line sits at the top of the chat and stays there while you scroll, for example "9 of 25 merged · 3 in progress: #4, #7, #9 · 1 needs you · next: #12 · about a third done". The server computes it from the same state the master reads. Progress is merged tickets over all tickets. The line updates whenever a ticket of the project changes.

The master runs in short beats and keeps a thread between them. Each beat keeps its reasons and the master's answer, not the ticket table, which it reads fresh every beat. Once the thread passes `dialogue.series_master_thread_max_chars` (default 60000 characters), the master writes notes in place of its older beats: what it decided and why, what is still open, the acts it took and its plan. The latest `dialogue.series_master_thread_keep_turns` beats (default 4) stay word for word.

The master costs money only when there is a decision to make. The server records everything that happens on the tickets for free and wakes the master only for a CI result (green, red, or a required build that never started), a CI that has run longer than `merge.ci_pending_seconds` without a result (once per head), a pull request merged, closed or reopened, a conflict with its base, a person's approval or change request, a comment, a ticket run that ended, a ticket that became the next merge, a series check that ended without a verdict, an answered question and your messages. A running CI, a new head, a conflict that cleared and a changed target are recorded and handed over with the next beat that has a decision in it. What the master's own merge or CI read recorded wakes nothing more, because its answer already told it. Events that arrive close together reach the master as one turn: it is woken once the oldest has waited `dialogue.series_wake_settle_seconds` (default 60). Your messages reach it at once. A series where nothing changes makes no model call at all. A run that dies is ended by the run reaper, and its end wakes the master like any other.

The master's reply is posted only when it acted (a retry, a message to a ticket, a pause, a question, the series check), when its own question was answered, or when you wrote to it. A look that changed nothing stays out of the conversation.

The head of the conversation reads **series running**, **· 1 question for you** while a question waits, and **series done** at the end. The **Series** tab in the right-hand pane opens by default while the series runs. It shows landed of total, elapsed time, cost, an estimate once two tickets have landed, what runs now, the critical path and the policy.

## Talking to it

Write in the same conversation. A message from someone holding `runs.control` goes to the master of the running beat, or the next one.

When the master asks a question with a recommended answer, or a ticket's run parks on a question with one, the question appears in the conversation. Answer it there. If nobody answers within `dialogue.question_default_minutes` (default 120), the series takes the recommendation, posts *Taken by default at HH:mm: …* and hands the answer on: to the master, or to the parked run, which resumes. A question without a recommendation waits for you.

## When you look: review

The project's `review` setting decides when a person looks at a series, and the series keeps the value it opened with:

- `end` (default): the series works on a series branch, and the master merges each ticket into it once its build and tests are green. You review one pull request per repository at the end.
- `each`: no series branch. Each ticket's pull request goes into the default branch, and the master merges it once a person approved it.
- `none`: no series branch. The master merges each ticket into the default branch on green; the host's branch protection still decides.

Whatever the review, the master's merge is checked first. A ticket whose prerequisite is not merged into its target yet (the series branch for a ticket of the series, the default branch for any other) is refused with that prerequisite named, and so is a pull request whose base the host could not report. The master can merge it anyway by giving a reason; a merge that went past a refusal names it with that reason in the master's act log, the ticket's last act and the conversation. A pull request that targets a branch other than the series or default branch is always refused until it is retargeted, because merging it there would record a landing that isn't one. Your own merge from the series page is not checked.

`series_status` and the series page show the series' review.

## The series branch

A series under `review: end` works on its own branch in every repository it touches, `agent-smith/series/<id>`, cut from the default branch by its first ticket there. Each ticket starts from that branch once the tickets it requires have landed on it, opens its pull request against it, and the series master merges it there. What else lands on the default branch meanwhile does not reach the series until its end. A ticket outside the series that requires one of its tickets waits until the series has reached the default branch. If a series pull request reports no CI, agent-smith starts its repository's pull-request CI ([CI loop](ci-loop.md#series-branches)).

When every ticket has landed on the series branch, one pull request per repository takes it into the default branch. Its description has a section per ticket: the ticket's title and pull request, what the master did on it and why, and what it could not verify, such as a CI on the ticket's last head that wasn't green or was never read. After the check passes, a person reviews and merges it, or asks the master to; a conflict with the default branch is the master's to refresh. The series is done once every one has merged. A closed one is said, and the branch is kept: only its final merge removes it.

A series opened before series branches existed keeps working ticket by ticket into the default branch. A series opened before the `review` setting keeps its branch or its lack of one, and runs as `end` with a branch, as `each` without.

## The check on main

Once every ticket has landed, a `series-verify` run checks out the series branch (the default branch for a series without one) and runs the specs' done criteria against the merged result. This is the cross-ticket check no single ticket's verification can make. Its sandbox is sized for a build, and the project's private-feed credentials are staged in it as for a code run.

- A criterion the check's environment cannot check (a secret it lacks, such as a disposable database's connection string, or a feed that still refuses after staging) is reported as *not checkable here*, with what would make it checkable. It is not a failure and files nothing.
- New failures become **one follow-up ticket**, filed and started in the same series.
- A check with nothing new closes the series with a summary: cost, duration, landed, open, and *Nothing waits on you.* when nothing does.

## Settings

| Key | Default | What it changes |
|---|---|---|
| `dialogue.series_wake_settle_seconds` | 60 | How long the oldest event may wait so events that arrive together are one master turn; 0 wakes it at once |
| `dialogue.series_status_minutes` | 15 | How long the master's events wait after its loop failed before it is tried again |
| `dialogue.question_default_minutes` | 120 | When a recommended answer is taken by default |
| `dialogue.series_master_budget_usd` | 20 | The series master's own spend |
| `dialogue.series_master_budget_ask_usd` | 2 | At or below this much left, the series asks to raise the budget or stop |
| `dialogue.series_master_thread_max_chars` | 60000 | Past this size the master's older beats become its notes; 0 turns it off |
| `dialogue.series_master_thread_keep_turns` | 4 | The latest beats a compaction keeps word for word |

They are under **Dialogue** in [Settings](../configure-it/settings.md). The full list is in the [agentsmith.yml reference](../reference/configuration/agentsmith-yml.md).

## Next

- [CI loop and merge policy](ci-loop.md): when each ticket merges.
- [Stacked start](stacked-start.md): when a dependent ticket starts.
- [Work it out](work-it-out.md): where the series reports.
