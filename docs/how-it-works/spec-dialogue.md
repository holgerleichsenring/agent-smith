# Spec dialogue

Agent Smith isn't only a pipeline you point at finished tickets. Since the p0315 series it's also the thing you talk to *before* the ticket exists: a design partner in your chat that discusses the work with you, drafts the spec, files the tickets, and then executes them through the same pipelines as everything else.

## Talking it through in chat

Start a thread in Slack or Teams and discuss what you want to build. Behind the thread sits a persistent spec-dialog session — keyed by platform and thread, stored in the relational database. That detail matters more than it sounds: the design transcript survives a Redis flush, a server restart, a weekend. You can have several threads going in parallel, each resumes exactly where it left off.

The agent on the other end is the `design-partner-master`. It answers questions *grounded in your actual code*: it has the cached project map and can open read-only source sandboxes per turn (read, list, grep — it gets refused if it tries to run or write anything). So "how does our auth middleware handle expired tokens today?" gets answered from the source, not from vibes.

## From discussion to filed work

When the discussion converges, the dialogue resolves to one of four outcomes:

- **An answer.** Sometimes talking it through *is* the work. Nothing gets filed.
- **A bug ticket.** Something's broken; a `fix-bug` ticket lands in your tracker.
- **A phase.** The partner drafts a schema-valid phase spec (the same YAML shape this project itself is built with — spec-first all the way down).
- **An epic.** N linked phase tickets; the parent is filed first, each child carries its `Parent:` reference and `requires:` edges to its siblings, and the parent gets one comment listing the slices.

`/create-phase` files the outcome into the active scope's tracker. Before anything is created you get a confirmation prompt as real Slack blocks / Teams cards — only an explicit approval files it, and replying with an edit instead re-runs the design turn with your correction. Ticket creation works on all four trackers (GitHub, GitLab, Azure DevOps, Jira) and comes back with the ticket id and its URL.

`/execute-phase` then runs a phase ticket through a phase-execution pipeline — or you just let the `phase` label trigger it like any other labeled ticket (that label is hard-wired; you don't need to map it in `pipeline_from_label`). Bugs stay on the normal `fix-bug` pipeline. The usual gates apply: the plan gets approved, the keystone still refuses to call a run successful without a real code change and green verification.

## The ticket is a conversation, not a string

When a run picks up a ticket, the master gets the whole thing:

- **The comment thread**, so an instruction clarified three comments deep actually reaches the agent. (GitLab system notes are filtered out; you don't want "changed the milestone" in the prompt.)
- **Image attachments** — screenshots of the broken UI, architecture sketches. Sent as image parts when the model has vision (`supports_vision` on the agent config, default true, capped at 10 images), noted as not-viewable otherwise.
- **Text documents** — a PDF or docx attached to the ticket is converted to markdown in the sandbox and included. One broken document doesn't fail the run; it's skipped with a note.

There's a contract on what the ticket text may tell the agent (p0316): in-scope instructions are followed; destructive or suspicious ones ("delete the test suite", "ignore your review rules") are refused *and logged* — `result.md` gets an `ignored_instructions` section, so a poisoned ticket is visible instead of silently obeyed.

## The clarification gate

The flip side of taking tickets seriously: refusing to invent scope when the ticket is too thin. A title-only ticket, an empty body, a planner that genuinely can't tell what's wanted — the run doesn't guess (p0318). It posts its open questions as a comment on the ticket and parks the ticket in your configured `needs_clarification_status`. You answer in the tracker; the run resumes with your answer. No half-baked PR, no burned tokens on invented requirements.

Mid-run questions work the same way — the run checkpoints and waits durably rather than holding compute. How that works is its own page: [Expectations & durable dialogue](expectations.md).

## Next

- [Expectations & durable dialogue](expectations.md) — the ask→checkpoint→resume loop and the ratified expectation contract.
- [Methodology](methodology.md) — what happens once the work is filed and triggered.
- [Trigger it: labels](../trigger-it/labels.md) — the `phase` label and friends.
