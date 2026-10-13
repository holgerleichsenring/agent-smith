# Work it out

Work it out is the dashboard page where you design a piece of work with Agent Smith before any ticket exists, or talk through a ticket that already does. You describe what you want to be true, the design partner reads the project's code and answers, and when the two of you agree it proposes a bug ticket, a spec or a cut of several specs. You approve it and it files the ticket. The rules of the conversation itself, the same in Slack, Teams and here, are on [Spec dialogue](spec-dialogue.md). This page covers what the dashboard shows you.

## Finding the page

**Work it out** sits in the dashboard's left rail and opens `/spec-dialog`. The page has three columns: your recent conversations, the exchange in the middle, and a pane on the right that shows what the conversation reads, proposes and filed. On a narrow screen the columns stack.

The page needs the `dialog.write` permission. A caller without it is told which permission is missing instead of being shown an empty page. See [Access control](../reference/security/access-control.md).

The page also takes a few query parameters, so other pages and your own bookmarks can link straight into a conversation:

| Parameter | What it does |
|---|---|
| `?open=<session>` | Opens that conversation. The conversations list writes these links. |
| `?ticket=<id>` | Opens the conversation that ticket already has, or gets ready to start one about it. |
| `?project=<name>` | Names the project the ticket belongs to. Without it the ticket's own labels decide. |

Each parameter is used once and then removed from the address, so reloading the page doesn't reopen something you've since moved away from.

## Starting a conversation

**+ New conversation** starts an empty one. A conversation reads one project's repositories, so the first thing it needs is a project. With several projects configured, the middle column asks "Which project is this about?" and offers a list. With exactly one project it doesn't ask.

Above that sits a search field, "Find a ticket by name or number". Type at least three characters and every configured tracker is searched. Each hit shows its key, its title, the tracker it came from and the tracker's own word for its kind (Bug, Task, User Story, whatever the board calls it; GitHub has no kinds, so it shows none). A hit whose number is exactly what you typed is marked "this number". A ticket that already has a conversation is marked "has a conversation", and picking it takes you to that one rather than starting a second. If a tracker couldn't be searched, or more tickets matched than are shown, the field says so.

Picking a ticket also picks the project, from the ticket's own labels on the tracker that holds it. If the labels name several projects, or none, the page tells you why and lets you choose. A ticket on a tracker that has no configured project can't be bound here, and the page says that instead of offering a project on another board. Picking a project by hand with no ticket in play clears the ticket search.

An empty conversation greets you, by name when your sign-in carries a readable one. The conversation actually opens when you send your first message.

## The composer

Type in the box at the bottom and press Enter to send (Shift+Enter for a new line). The **+** button, labelled "Attach", opens a menu with **Image**, which takes a PNG, JPEG, GIF or WebP file. Use it for a screenshot of the broken screen or a sketch of what you want. The image is stored with the conversation and shows up in the exchange. Each turn is shown the four most recent images the conversation holds. They survive a reload and a resumed conversation. You can attach an image before you've written anything; the conversation opens to keep it.

## While a turn is working

A turn takes anywhere from seconds to a few minutes, so the exchange shows what it's doing instead of a spinner and nothing else. The working line says what it's opening: "Reading the ticket…", "Opening the repositories it needs…", or "Working it out…" once everything is open. Beside that, the elapsed time and the number of steps taken. Under it:

- one line per repository (and the bound ticket) with its state: opening, ready, or could not be opened;
- the latest three steps: which tool read what, "thinking", "reviewing its own proposal against the code", "revising its answer". Older steps fold into "N earlier steps".

If you leave and come back to a conversation mid-turn, the working line is still there.

## Recents and the conversations page

The left column, **Recents**, lists your twenty most recent conversations, newest first. Each row shows the conversation's subject (the model names it after the first exchange; until then the row shows your first line), the project, the bound ticket if any, the turn count, when it was last active, and what it filed ("spec filed · 1 ticket"). Click a row to open it; a closed conversation is resumed where it left off.

**All conversations** opens `/spec-dialog/conversations`, a full-width list of up to 200 conversations with a count ("12 in all", or "the 200 most recent of 340"). A conversation that is open somewhere is marked "open". Every row links to its own address.

### Who a conversation belongs to

A conversation you start with no ticket is yours: only you can read it, write in it or approve what it proposes. A conversation bound to a ticket is different. A ticket has exactly one conversation, and anyone who can use this page can open, resume and continue it, because everyone who can see the ticket on the board may discuss it. Deleting a conversation is always the owner's alone.

### Deleting

The **×** on a row deletes the conversation, after a confirmation that spells out what it doesn't undo. The conversation and every answer you gave in it are gone, and there is no undo. The tickets it filed stay in the tracker with their approved specification; only the link from this conversation to them stops resolving. An empty conversation with no turns is deleted without asking.

## The pane: Scope, Proposal, Filed, Approved spec

The right-hand pane has up to four tabs. It picks the most useful one on its own: the filing once there is one, the proposal once there is one, the scope before that. You can switch at any time.

**Scope** shows what the conversation may read. For a ticket conversation it starts with **Ticket**: the key, the title, when it was read, and the exact text the conversation was given (marked when the ticket was longer than the conversation carries). Then the project with its **Repositories** and **Templates**, each template with the repository and revision it comes from. Before a conversation is open, it lists what a new one could be grounded in.

**Proposal** shows the latest proposal as a structure, not as a wall of YAML. A bug is a title and a body. A spec is its goal, steps, tests, "Done when" and "Waits for". A cut is the work ticket plus its slices in order, each with what it waits for; the first slice is expanded. "The same thing, as it will be filed" folds out the raw YAML. The proposal card in the exchange has an **Inspect →** link that brings its proposal up here, which is how you look at an older proposal after a newer one replaced it (the tab then says "superseded").

**Filed** follows what the conversation filed. For each ticket: its key and title (linked to the tracker), whether it was started and why or why not, and then its runs, live. Each run shows its status, its id (linked to the run page), project and cost, a pending question if the run is waiting on one, its pull requests with their state, and its specs one by one with their status, the verification verdict and what the review found. A spec handed back because the specification itself was wrong says so and tells you to approve the set again in this conversation. For a ticket conversation, this tab shows every run on the ticket, including ones this conversation didn't start. The tab needs `runs.read` as well as `dialog.write`.

**Approved spec** appears on a ticket that has one. It shows the specs a person approved, who approved them and when, and says plainly that this is what was ratified, not necessarily what a run is working on right now.

## Approving

When a proposal is ready, a card in the exchange says "waiting on you" and summarises what would be filed ("File this spec? One ticket.", "File this epic? One work ticket carrying 3 slices."). If the in-turn review found problems, they're listed on the card with their evidence. The buttons:

- **Approve & file** files it.
- **Reject** files nothing.
- One or more other shapes, such as **Cut into several specs** or **Make it a bug ticket**. Picking one files nothing; it starts a new turn in that shape, and you approve the result.

Anything you type instead is taken as a note, and the proposal is revised with it. Your decision is recorded in the exchange ("Approved — file the proposal", "Rejected — nothing is filed"). The card waits fifteen minutes. After that it says "no longer waiting — nothing was filed", and your next message starts a fresh turn.

Approving files the ticket and tries to start its run. Moving a ticket into a trigger status starts a run, so that part needs the `runs.control` permission; without it the ticket is filed and the Filed tab says which move in the tracker would start it. What filing writes, and every reason a ticket may not start, is on [Spec dialogue](spec-dialogue.md#what-approval-files).

## Talking about an existing ticket

Bind a conversation to a ticket (search for it, or open `?ticket=`) and the ticket becomes the conversation's subject. Every turn is given the ticket's text as the requirement being discussed, never as instructions to the agent. Long tickets are cut at comment boundaries with the description kept whole, and the design partner can read the rest in slices when the answer depends on it. If the ticket changes on the tracker after the conversation read it, the next turn says so before relying on the old text. If the ticket already has an approved specification that says things the ticket text doesn't, the conversation raises that and asks which is right.

The design partner can also read what the framework itself did about the ticket, kept apart from what the tracker shows. For each of the ticket's ten newest runs it sees the pipeline, the status, why the attempt started (a ticket comment, a pull request, a status move, a request, a retrigger, a pickup, an approval in a conversation, or a retry, with who and when), and a parked run's question. For each spec a run worked it sees the status, the verdict, and the outcome of every criterion with its note and citation, plus criteria the agent declined and accounts nobody could take, such as a red build. It also sees every pull request recorded for the ticket by its address, with its state and the newest 40 review entries across them: comments, reviews and decisions with their author, file and line. An entry from an author who isn't trusted is listed but started nothing, and a system entry is the host's own event rather than a person's. So you can ask which criterion failed, who requested changes and on which line, or why the last attempt started, and the answer names the record it rests on.

Approving a spec or a cut in a ticket conversation doesn't file a new ticket. What it does depends on the ticket, and the approval preview (`GET /api/tickets/{project}/{ticketId}/approval-preview`, which needs `dialog.write`) shows it before you approve, spec by spec:

- **A ticket a run has worked** is continued by spec id. A spec that already ran is kept exactly as it ran; a changed text of one is reported as discarded. A spec that hasn't run and that the proposal repeats is re-cut. One that a lone spec leaves out is kept, and one that a cut leaves out is dropped. A spec with a new id is appended after the rest. The approval is recorded under the ticket, and the ticket's next run applies it to the series on the branch and names anything that changed there since. Once approved this way, the series is final for ticket text: a comment or an edit on the ticket no longer re-cuts it, so changes go through the conversation again. "Worked" means a code run on any project that shares the ticket's tracker; a pull-request review or a scan doesn't count.
- **A ticket no code run has worked** takes the approved series whole, on GitHub, GitLab, Azure DevOps and Jira: it's recorded under the ticket and written to the ticket branch. The framework's part of the description is rewritten from it where the tracker supports that (GitHub, GitLab, Azure DevOps). Jira refuses, because its description comes back as plain text and rewriting it would destroy what a person wrote; the conversation says so, and the series is recorded anyway. While a run holds the ticket, the approval is refused and nothing is recorded.
- **A ticket a person closed** gets one follow-up ticket with the proposal's specs that haven't already run on it, and a comment on each ticket names the other. A ticket the framework itself moved to done or failed isn't closed in this sense and is continued instead. The conversation stays the closed ticket's, so a later approval there files another follow-up.

If the tracker can't be read, the approval is refused and nothing is recorded; approve again once it answers. A series that would hold more than eight specs is refused with both counts.

### When the next attempt starts

Approving in a ticket's conversation also starts the work, if you hold `runs.control`. The approval is the act: the attempt it starts records it as its cause, with who approved, when, and in which conversation. The conversation says which of these happened, and the approval preview shows the same answer for you before you approve:

- **The last attempt ended.** The next attempt starts now, on whichever server picks up the request.
- **A run is working the ticket.** The approval waits for that run. When it ends, the next attempt starts, unless the run already picked up this approval and worked it.
- **The last attempt handed the ticket back.** The approval answers the hand-back: the waiting run is closed and the next attempt starts. A keyword comment written after the approval doesn't change that.
- **The last attempt is waiting for an answer to its question.** Nothing is recorded. The conversation names the run and where to answer it (the run's question, or `POST /api/runs/{runId}/answer`). The proposal stays open, so you can approve it once the question is answered.
- **No code run has worked the ticket yet.** Its first attempt starts the way a filed ticket's does. The project's routing tag is put on the ticket and the ticket is moved into a trigger status, so the next poll picks it up.
- **You don't hold `runs.control`.** The change is recorded and nothing starts, not even at the next run end. Someone who may start runs, or a move on the tracker, starts the next attempt.

An operator's stop (a cancel or a delete) withholds an approval given before it, as it does any other act.

## Withdrawing a filing

If you filed something by mistake, say so in the conversation. The design partner can close a ticket this conversation filed, as long as nothing has claimed it yet, and the Filed tab then shows it as withdrawn with your reason. Once a run holds the ticket, the withdrawal is refused and you stop the run from the run controls instead.

## Next

- [Spec dialogue](spec-dialogue.md): how the conversation behaves on every channel, and what approval files.
- [Trigger it: labels](../trigger-it/labels.md): the labels a filed ticket carries.
- [Dashboard](../reference/operations/dashboard.md): the rest of the dashboard.
