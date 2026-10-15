# Ticket dependencies

Some work doesn't fit one ticket and one branch. The schema change has to be merged before the API on top of it makes sense, and the screens want the API merged before anybody builds them. A cut of specs on one ticket puts all of that on one branch and one pull request, which is fine until a reviewer wants to merge the first part on its own. For that case Work it out files a list of tickets instead, and agent-smith records which ticket requires which. A ticket that requires another waits until that one has landed, and then it starts by itself.

`requires` here is between tickets. The `requires:` edges between the specs of one ticket's series are a different thing, described on [Spec dialogue](spec-dialogue.md#discussion-first-proposal-after).

## Where requires come from

Requires come from a list of tickets proposed in [Work it out](work-it-out.md#a-list-of-tickets), in a conversation that isn't bound to a ticket. The design partner proposes 2 to 20 ticket texts. Each has a key inside the list, a title, what it must achieve, its done items, the repositories it touches, and what it requires: another ticket of the same list by its key, or a ticket that already exists in the project's tracker, written `ticket:41` (or `ticket:#41`). The list is put in merge order before you see it; a list whose requires go round in a circle is refused and the design partner is asked again with the reason.

A ticket in the list carries no specs. The run that starts it derives its specs from the ticket's text, against the code that has been merged by then. That's the point of waiting: the second ticket is cut against the first one's merged result, rather than against a plan of it.

## Filing a list

Approving the list files it, in merge order:

1. Every `ticket:<id>` the list requires is read on the tracker first. One the tracker doesn't have, or can't read, refuses the whole list before anything is created.
2. Each ticket is created as a work ticket: its work item type comes from the tracker's `work_item_kinds` map under the `work` role, and it carries the same labels as an approved spec. Its body is its text: what it must achieve, the done items as acceptance criteria, the tickets that must merge first, and the conversation it came from.
3. Its requires are stored in agent-smith's database, and with them the conversation's uploads and images, so the ticket's run gets the same material.
4. Last, the approval of its text is stored. That record is the mark that the ticket's filing is complete.

If filing stops halfway (a tracker error, a lost connection), the tickets already created stay and the filing names the ones that weren't. A ticket that was created but whose filing didn't complete is never started; the Filed tab tells you to close it, and an attempt to start it anyway is refused with "This ticket's filing did not finish; close it and approve the list again."

Then each ticket's start is decided. A ticket with nothing left to wait for starts the way any filed ticket does: the routing tag goes on, and with `runs.control` it's moved into a trigger status. Every other ticket is held, and the Filed tab says what it waits for ("waits for #12 to be merged or marked done by hand").

### What the tracker shows

Each requires is also written to the tracker, so people on the board see the order:

| Tracker | Link |
|---|---|
| Azure DevOps | A Dependency link, with the required work item as the predecessor. |
| Jira | A Blocks link: the required issue blocks the dependent one. |
| GitHub | GitHub's own "blocked by" dependency (github.com, and GitHub Enterprise Server 3.20 and later). Where that API isn't available, one comment on the dependent issue: "Agent Smith — requires #12 (must merge first)". |
| GitLab | A related-issue link (`relates_to`). GitLab's "blocks" link needs a paid tier, and the related link is the one every tier offers. |

A link the tracker refuses doesn't stop the filing; it comes back as a filing note naming the kind of failure. The tracker link is there for people. The order agent-smith enforces comes from its own stored requires, so editing or deleting the link on the board changes nothing about when a ticket starts.

## Landing

A requirement is met when the required ticket has landed. There are two ways to land:

- **Merged.** The ticket's pull requests have all ended and at least one of them merged. The first time agent-smith sees that, it writes a landing record for the ticket, once. The record is permanent: deleting the ticket's runs doesn't remove it. Every five minutes a re-read goes over merged tickets with no landing yet, so a merge whose webhook never arrived still lands.
- **Marked done by hand.** Sometimes the required work was finished somewhere agent-smith couldn't see it: shipped by hand, solved in another ticket, dropped on purpose. **Mark done by hand** on the ticket page or in the graph view records a landing with your name and a required note. It needs `runs.control`, because it can start every ticket that waited, and it can't be undone. A `ticket:<id>` from outside the list lands the same two ways; when its work merged somewhere agent-smith doesn't watch, this is the way. The [dashboard page](../reference/operations/dashboard.md#marking-a-ticket-done-by-hand) has the details and the HTTP call.

## The merge gate

The gate is strict. No code run starts on a ticket while a ticket it requires hasn't landed, whichever way the start comes: a poll or a webhook pickup, a keyword comment, a status move, a retry, a request in the ticket's conversation, a start by name, or the series starting its next attempt. A pickup of a held ticket does nothing and leaves it where it is. A person's act on it gets the reason, naming the tickets it waits for: "This ticket waits for #12, #14 to be merged first; merge them, or mark them done by hand." In the ticket's conversation, asking to start it names what it waits for and queues nothing.

The gate is about merged code, so it holds code runs only. A scan, a pull-request review or a design conversation on a held ticket goes ahead.

## The automatic start

When the last requirement of a held ticket lands, agent-smith starts the ticket by itself, the way a freshly filed ticket starts: tagged, moved into a trigger status and claimed. That happens only when the person who approved the list held `runs.control` at the time, because the start is still theirs. Without it, the ticket stops waiting and sits under **Filed, no run on record** until someone moves it into a trigger status. A ticket a person closed, or one marked done by hand, is never started.

The wake is durable. Only the server whose landing record won writes the wake, and a wake lost to a crash is recovered by the same five-minute re-read, for landings of the last 24 hours.

## What you see

On the dashboard's ticket list, a filed ticket that hasn't run sits in one of two sections: **Waits for another ticket** while something it requires hasn't landed, and **Filed, no run on record** once nothing holds it. A landed ticket counts as **Done**. The list's **Graph** view (`/tickets?view=graph`) draws every ticket that requires or is required, with an arrow from each ticket to what it requires and each node in its state.

A ticket's own page shows **Waits for**, each required ticket with "(not merged)", "(merged)" or "(done by hand)" and who marked it, and **Required by**, the tickets waiting for this one. A ticket that never ran says "Filed, no run on record" and links to its conversation in Work it out, where you can settle its specs before its first run. The [Dashboard](../reference/operations/dashboard.md#tickets) page describes all of it.

## Next

- [Work it out](work-it-out.md): proposing and approving a list of tickets.
- [Spec dialogue](spec-dialogue.md): what approval files, and the conversation's rules.
- [Dashboard](../reference/operations/dashboard.md#the-graph-view): the graph view and marking a ticket done by hand.
