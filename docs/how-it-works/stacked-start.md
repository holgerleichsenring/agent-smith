# Stacked start

<p class="cb-lead">A ticket that requires another one doesn't have to wait for that one to merge. By default it starts as soon as its prerequisite's run is green, on top of the prerequisite's branch.</p>

## When a dependent starts

Each project picks one rule, `requires_start`:

| Value | A dependent ticket starts when |
|---|---|
| `on_green_push` (default) | every ticket it requires has a newest code run that ended green and opened its own pull request |
| `on_merge` | every ticket it requires has landed: merged, or marked done by hand |

A prerequisite counts as green only if its newest attempt finished successfully, did not stop at a series boundary, opened a pull request itself, and the ticket isn't closed. Set it under **requires start** on the project's merge tab in the [Config studio](../configure-it/config-studio.md), or as `requires_start:` on the project.

Whatever triggers the dependent (its label, a poll, a person, the [series run](series-run.md)), the same check decides. A refused start says which prerequisite isn't green yet.

## What a stacked run does

<ol class="cb-stages">
<li><strong>Cut from the prerequisite.</strong> The dependent's branch is cut from the prerequisite's branch, so it builds on code that isn't merged yet.</li>
<li><strong>Target the prerequisite.</strong> Its pull request targets the prerequisite's branch, so the diff shows only its own work.</li>
<li><strong>Merge it in again.</strong> If the prerequisite's branch moved (a fix run, a rework), the dependent's next run merges it in.</li>
<li><strong>Retarget on landing.</strong> When the prerequisite lands, a <code>retarget</code> run moves the dependent's pull request onto the base the prerequisite landed on.</li>
</ol>

A dependent that has started once is not held again: its rework is never refused while the prerequisite reworks. A missing prerequisite branch is never recreated.

After a squash merge the moved pull request still shows the prerequisite's commits until the dependent's next run merges its new base.

## On the ticket list

A never-run dependent reads **ready** as soon as its prerequisites are green under `on_green_push`, and stays **blocked** under `on_merge` until they land. The graph view (`/tickets?view=graph`) draws the same state. How `requires` links are filed, and how to mark a ticket done by hand, is on [Ticket dependencies](ticket-dependencies.md).

## Next

- [CI loop and merge policy](ci-loop.md): when the prerequisite merges.
- [Series run](series-run.md): who starts the tickets of a filing.
