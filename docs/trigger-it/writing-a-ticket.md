# Writing a ticket

A ticket that Agent Smith works on is read as free text, so any ticket a person can understand will do. The shape below is the one the framework files itself when a design conversation is approved, and it's the one that reads best: for the team reviewing the ticket, and for the run that derives a phase spec from it.

## The template

Copy this into the ticket's description and replace each part:

```markdown
One sentence saying what is true once this is done.

## Why
- The reason this is worth doing, and what the choices behind it are.

## What changes
The behaviour, screens or interfaces that change, in a few sentences.

## Acceptance criteria
- The export lists every order of the selected month
- GIVEN an order without a customer WHEN the export runs THEN the order is listed with an empty customer column

## Out of scope
What this ticket deliberately leaves out, so nobody builds it by accident.
```

- **The first line is the goal.** It has no heading. Say what is true afterwards, not what to do.
- **Why** holds the reasons. A reviewer who disagrees with one can say so before any code exists.
- **What changes** says what a user or a caller will notice. It doesn't say how to build it.
- **Acceptance criteria** is one list item per criterion. When a criterion has a trigger and an observable result, write it as one line in plain words: `GIVEN` the starting point (optional), `WHEN` the trigger, `THEN` the result. Keep each criterion on its own line and don't nest `WHEN` and `THEN` as sub-items.
- **Out of scope** names what is left for later, so the run doesn't take it on.

## Why there is no steps section

A ticket says what is wanted, not how to get there. The run works out the steps from the repository it reads. When a ticket lists steps, each one is read as something the result must show, and a correct change that took a different route fails its own ticket.

## What is read as items

Only the list under **Acceptance criteria** is read item by item. Each list item is one criterion the run is held to, and the pull request reports against each one. An indented line under an item belongs to that item. Prose, comments and code under the heading aren't criteria. The rest of the ticket is read as text.

On Azure DevOps, a ticket's **Acceptance Criteria** field takes precedence when it's filled in. One criterion per line there.

A bug ticket keeps its own shape: what is wrong, and what correct looks like. Its criteria go under the same **Acceptance criteria** heading, one list item each.
