# Spec dialogue

Agent Smith isn't only a pipeline you point at finished tickets. It's also the thing you talk to *before* the ticket exists: a design partner that discusses the work with you, drafts the spec, files the ticket, and then runs it through the same pipelines as everything else.

## Where you talk to it

You can hold the conversation in three places, and it behaves the same in all of them:

- **The dashboard.** The [Work it out](work-it-out.md) page, with a project picker, a ticket search, a live view of what each turn is doing, and a pane that follows what you filed. It's the fullest surface, and the only one that can talk about an existing ticket.
- **Slack or Teams.** Send `@Agent Smith /spec my-project` in a channel. The conversation lives in that message's thread (in Teams, in that conversation), and every further message there goes to it; a new thread starts a separate one. `/spec` without a project works when only one project is configured, and lists the projects otherwise.

Each conversation is a session stored in the relational database, so the transcript survives a Redis flush, a server restart, a weekend. A conversation is scoped to one project and reads that project's repositories.

## What grounds the answers

The agent on the other end is the `design-partner-master`. It answers from your actual code, not from vibes:

- Each turn starts with the project's principles and context files, read straight from the repositories without a sandbox.
- For anything deeper it opens read-only source sandboxes, one per repository. It can read, list, grep and search files, and fetch over HTTP through processes the server builds itself. It gets no shell, and nothing in a source sandbox is writable.
- Sandboxes are held between turns, so only the first message of a conversation pays for spawning a container and cloning. The hold defaults to 180 seconds, set by `sandbox.hold_seconds` (or `SANDBOX_HOLD_SECONDS`) and overridable per project. A held sandbox is given up before a pipeline run is refused capacity. See [Sandbox architecture](../reference/concepts/sandbox-architecture.md).
- A turn can fan out into read-only child agents to look at several things at once, bounded by `max_sub_agents_per_dialog_turn` and `max_dialog_sub_agent_loop_iterations`. See [Multi-agent orchestration](../reference/concepts/multi-agent-orchestration.md).

## Discussion first, proposal after

The first reply to a new piece of work is always a discussion: what it found in the code, the edge cases, the open questions. It won't propose anything until you've answered. Once you have, and the conversation has converged, a turn may end in one of four outcomes:

- **An answer.** Sometimes talking it through *is* the work. Nothing gets filed.
- **A bug ticket.** Something's broken; a bug ticket lands in your tracker.
- **A phase.** A schema-valid phase spec, the same YAML shape this project itself is built with.
- **An epic.** A cut into several phases, in order, each with its `requires:` edges. It's still one piece of work: it files one ticket, and one run works the phases in order, a successor never starting before its predecessor verified.

A phase or epic proposal is reviewed inside the turn that made it, by a fresh instance reading the same repositories. What the review finds is shown next to the proposal when you're asked to approve it, with its evidence. A review that couldn't be taken blocks nothing.

## Confirming

Before anything is created you're asked, as Slack blocks, a Teams card, or a card on the dashboard. You can:

- **approve** it, which files it;
- **reject** it, which files nothing;
- pick another shape. A bug offers **Make it a phase**; a phase offers **Cut into several phases** and **Make it a bug ticket**; an epic offers **Make it one phase**. In a conversation about an existing ticket, phases and epics also offer **Amend this ticket** (see [Work it out](work-it-out.md#talking-about-an-existing-ticket)). Picking a shape files nothing. It starts a new turn in that shape, and you approve the result;
- reply with anything else, which is taken as a note, and the proposal is revised with it.

If the buttons can't be shown, a text reply in the thread still works. The question waits fifteen minutes; after that nothing is filed and your next message starts a fresh turn.

## What approval files

Approval files exactly one ticket into the project's tracker, on all four trackers (GitHub, GitLab, Azure DevOps, Jira), and reports its key and URL. For a phase or an epic, in this order:

1. **The ticket.** It carries one label, `phase-spec:approved` (a tracker can rename it, see [labels](../trigger-it/labels.md#labels-the-framework-writes)), and a short note in its body explaining what that label means. The note is removed before any model or pull request reads the ticket. Its work-item type comes from the tracker's `work_item_kinds` map, keyed by filing role: `phase` for a single phase, `work` for an epic's ticket, `bug` for a bug. Without a mapping the tracker's default type is used.
2. **The approved set.** The server records the approved phases against the ticket, and writes them to the ticket's branch under `.agentsmith/specs/<provider>-<ticketId>/`, with a `set.yaml` index. If that branch write fails, the filing still stands and says so; the run that claims the ticket publishes the set from the stored record.
3. **The routing tag.** When the project resolves tickets by tag, that tag is added so the poller routes the ticket to this project. An empty value, or one the tracker's label syntax can't carry, is reported instead of sent.
4. **The start.** If routing would claim the ticket, it's moved into the project's first trigger status, which starts the run. On the dashboard that move needs the `runs.control` permission. An approval from Slack or Teams never moves a ticket. Either way, a ticket created in a status that already triggers starts on its own. When the ticket isn't started, the filing says why and what would start it.

A bug is filed the same way without labels and without an approved set: the ticket, the routing tag, the start. It runs the `code` pipeline from its own text.

A run on a filed phase or epic works the approved set as it stands. It doesn't derive a spec of its own. If there's no set on the branch and no stored record, the ticket is parked, and the park names the path it looked at. A person can still type the `phase` label onto a ticket to route it the same way.

## Changing what was approved

Comments and edits on the ticket never re-cut an approved set. There are two deliberate ways to change it:

- **Approve again in the conversation.** In a ticket conversation on the dashboard, **Amend this ticket** rewrites the ticket from the new specification and records it as the approved set.
- **Ask on the ticket.** A comment that opens with the line `cut this specification again` makes the next run cut the specification again. It counts when it's newer than the last approval and than the run's last "this is how I understood the ticket" comment, and it's for people who can reach the ticket but not the branch. The new cut is recorded as approved by whoever wrote that comment.

A conversation that has filed work is told, every turn, that it cannot change, close or re-cut what it filed, and that describing filed work as changed would be false. The one thing it can do is withdraw: close a ticket it filed that nothing has claimed yet. Once a run holds the ticket, that's refused and you stop the run from the run controls.

## The ticket is a conversation, not a string

When a run picks up a ticket, the master gets the whole thing:

- **The comment thread**, so an instruction clarified three comments deep actually reaches the agent. GitLab system notes are filtered out; you don't want "changed the milestone" in the prompt.
- **Image attachments**: screenshots of the broken UI, architecture sketches. Sent as image parts when the model has vision (`supports_vision` on the agent config, default true, capped at 10 images), noted as not viewable otherwise.
- **Text documents.** A PDF or docx attached to the ticket is converted to markdown in the sandbox and included. One broken document doesn't fail the run; it's skipped with a note.

There's a contract on what the ticket text may tell the agent. In-scope instructions are followed. Destructive or suspicious ones ("delete the test suite", "ignore your review rules") are refused and logged as ignored instructions, so a poisoned ticket is visible instead of silently obeyed.

## The clarification gate

The flip side of taking tickets seriously: refusing to invent scope when the ticket is too thin. A title-only ticket, an empty body, a planner that genuinely can't tell what's wanted: the run doesn't guess. It posts its open questions as a comment on the ticket, mentions the assignee (or the reporter, if nobody is assigned) so someone gets notified, and parks the ticket in your configured `needs_clarification_status`. You answer in the tracker; the run resumes with your answer. No half-baked PR, no burned tokens on invented requirements.

Mid-run questions work the same way: the run checkpoints and waits durably rather than holding compute. How that works is its own page: [Expectations & durable dialogue](expectations.md).

## Next

- [Work it out](work-it-out.md): the dashboard page for these conversations.
- [Expectations & durable dialogue](expectations.md): the ask, checkpoint, resume loop and the ratified expectation contract.
- [Methodology](methodology.md): what happens once the work is filed and triggered.
- [Trigger it: labels](../trigger-it/labels.md): the labels a filed ticket carries.
