# How a spec is checked

Every spec ends in a "done when" list. Two things decide what becomes of it: the repository's own build and test stages, which either pass or don't, and the coding master's report, which says in its own words what was delivered.

This is how a Claude Code session works when one agent leads and another builds. The master never writes the spec's code itself. It briefs a sub-agent, looks at what that sub-agent did, sends work back until it holds, and then reports. The master knows the goal and didn't write the code, which is why it does the checking.

## The master leads, a sub-agent builds

The master starts a sub-agent with a brief: the goal and why it matters, the spec's steps and done list, what it already found (with paths), what is decided and what is still a guess to verify, the constraints, and what to report back. The sub-agent has none of the master's conversation, so the brief has to carry everything.

While the sub-agent works, the master can write to it; the sub-agent reads the message with its next tool result. When it finishes, its answer reaches the master the same way. The master then looks for itself: it reads the diff and the files, builds and runs the tests, and runs what the spec changed where the sandbox can. It goes through the done list one line at a time, then the coding principles, then whether the diff does what the spec says and nothing it doesn't.

Anything missing goes back to the sub-agent with what the master saw and what a good result looks like. The sub-agent keeps its own conversation, so it picks up where it was. Small things in scope the master fixes itself or has fixed.

## The report

The master ends on **its report**: its last reply, written in markdown for the person who accepts the work — what the spec delivered and how the master saw it (paths, commands and their outcome), what it fixed in passing, what is still open and why, what it did not check itself, the follow-ups it proposes, any ticket instructions it refused, the build note for the next spec (what it renamed, moved, persisted and deviated from) and the memories it relied on. It calls no closing tool. The report is shown as written.

After the master ends, one structured read takes the follow-ups (a one-line goal and why each), the refused instructions, the build note and the memories off the report. Nothing it reads is invented: a refused instruction is kept only when its quote appears in the report or the master's decisions, and a memory only when the store holds it. When the read fails, the report still stands and the run says the details could not be read; the read never decides delivery.

A line left open is the master's call, weighed by what it costs the spec's and the ticket's goal: would the person accepting this hold it back for it? Nothing is judged per done line, and what the report says about its own outcome is shown, never acted on. You decide whether a follow-up becomes work. A question only a person can answer goes through `ask_human`. A master that leaves checklist steps open gets one more pass for them; a master that wrote no report at all leaves nobody's word on the spec, and the spec fails and says so.

## The build and test stages

After the master, `VerifySpec` runs the verify stages each repository declares in its context — build, test, lint, whatever the repo wrote down ([the `verify` block](../reference/concepts/context-file.md#verify-is-what-proves-a-change)). This is the only thing that blocks a spec, like a commit gate.

A red stage goes back to the master with its output; the master hands it to its sub-agent. It goes back again while each repair changes the code and the failure, and not once the run's cost budget is spent. If the same stage fails the same way after a repair, or the repair changed nothing, the spec fails, and the run says so honestly. What that means for the run is on [Lifecycle](lifecycle.md#when-it-goes-wrong).

A repository whose contexts need two toolchain images is worked in two sandboxes. Before a stage runs, every sandbox of one repository is brought to the union of their work. When two sandboxes changed the same file to different contents, nothing is applied and verify is red, naming the file. A run where a change was delivered and no repository ran any verify command over it fails too: nobody checked what shipped.

## Where you read it

- The run page in the [dashboard](../reference/operations/dashboard.md): each spec's section shows the master and its sub-agent at work, the messages the master sent and where work went back, and then the report.
- The ticket page shows each spec's report and lists the proposed follow-ups across the run.
- The pull request body and the spec record committed to the branch carry the report too.

Runs from before this way of working show what they stored: the independent account with its "N of M proven" rows, or the master's per-line report with its "this verdict is wrong" link. They still read as they were stored.

## Next

- [Lifecycle](lifecycle.md) — where verification sits in a run.
- [Expectations & durable dialogue](expectations.md) — where the "done when" list comes from.
- [The context file](../reference/concepts/context-file.md) — declaring the verify stages.
