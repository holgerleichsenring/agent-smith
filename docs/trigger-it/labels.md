# Trigger: labels

Labels are how a ticket says "yes, run Agent Smith on this, with this pipeline". The mapping from label to pipeline lives in `pipeline_from_label`, either on the tracker, where every project routed to it inherits it, or on a project's trigger block when one project needs its own map.

On a server you edit that in **Configuration → Trackers** or the project drawer. The YAML below is the same thing, and what the CLI reads.

## The label-to-pipeline mapping

```yaml
projects:
  azuredevops-todolist:
    # ... agent, tracker, repos ...
    azuredevops_trigger:
      pipeline_from_label:
        agent-smith:init:               init-project
        agent-smith:bug:                code
        agent-smith:feature:            code
        agent-smith:security-scan:      security-scan
        agent-smith:api-security-scan:  api-security-scan
```

Tag a work item / issue with `agent-smith:bug`, and the framework runs the `code` pipeline. Tag with `agent-smith:security-scan`, the `security-scan` pipeline. First match wins — labels are checked in declaration order, case-insensitively.

The label values themselves are arbitrary; the convention is `agent-smith:{pipeline-name}` because it groups them together in the tracker's label UI. You can rename them if your team prefers (`smith-fix`, `ai-fix`, whatever) — the YAML key is the label, the YAML value is the pipeline. The one restriction: a routing word can't be the same as a label the framework writes (see [below](#labels-the-framework-writes)).

Any pipeline a ticket can run is a valid value, `init-project` included: the label starts it on the ticket's branch. `spec-dialog` is the exception. A design conversation starts from the chat or the dashboard, with a transcript and a place to reply that a ticket doesn't carry, so a label can't start one. Config Studio refuses to save a label or a default pipeline that names it, and names the label; a configuration file or stored configuration that still has one gets an advisory finding at startup.

## What if no label matches

If a ticket comes in (via webhook or poll) and enters one of `trigger_statuses`, what happens next depends on whether a map exists:

- **A `pipeline_from_label` map exists and none of its labels are on the ticket.** The framework ignores the ticket. No run gets started, no label gets written. This is intentional — labels are how the human (or some upstream automation) explicitly opts a ticket in.
- **No map at all.** Every ticket that reaches a trigger status runs the `default_pipeline` declared on the project's trigger block, or inherited from the tracker. With none declared, it runs `code`.

Two reasonable patterns for opting in with a map:

- **Manual.** A developer tags `agent-smith:bug` when they want Agent Smith to fix it. Everything untagged stays human-only.
- **Default-on.** A workflow rule in the tracker auto-applies `agent-smith:bug` to every ticket of type Bug when it moves to Active. Now Agent Smith picks up every bug; the human can remove the label to opt out.

Both work. Default-on gives you the volume but you'll see more `agent-smith:failed` labels when the agent can't figure out a ticket. Manual is friendlier when you're still learning the agent's strengths.

## Phase tickets

A ticket that carries an approved specification runs the `code` pipeline and works that specification, whatever `pipeline_from_label` says. The check runs before the map. A ticket counts as one when any of these is true:

- It carries `phase-spec:approved`, the stamp a [spec dialogue](../how-it-works/spec-dialogue.md) filing writes, or the name the tracker gives that stamp (see [Renaming them](#renaming-them)).
- The server holds an approval record for it. This still routes the ticket after someone removes the stamp; the stamp is a hint for people scanning the board, and the record is what binds. On Jira and Azure DevOps the poller also finds approved tickets by id, so a ticket whose stamp was removed is still picked up (up to 50 outstanding approvals per tracker; the rest wait, with a warning, until older ones are done or the stamp is put back).
- A person typed the `phase` label onto it.

Two more labels come from an older shape of filing, where an epic was filed as a parent ticket and one child per slice. Nothing writes them any more, but they still mean something:

- `phase-epic` marks a record ticket, not work. A ticket carrying it is never routed.
- `phase-parent:<id>` names a ticket's epic parent. The run reads the parent ticket's text as background for the spec, cuts its branch from the parent's branch, and opens its pull request against it. See [Branch persistence](../reference/concepts/branch-persistence.md#parent-branches).

## Labels the framework writes

In addition to the trigger labels you set, Agent Smith writes a lifecycle label to show where the run is:

| Label | Meaning |
|---|---|
| `agent-smith:pending` | New triggered ticket, eligible to be claimed. |
| `agent-smith:enqueued` | The ticket is claimed, the run is queued. |
| `agent-smith:in-progress` | The pipeline is running. |
| `agent-smith:waiting` | The run is parked, waiting for a person to answer. |
| `agent-smith:done` | Pipeline finished successfully. |
| `agent-smith:shortfall` | The run delivered the phases it verified and fell short of the rest. The ticket counts as done, not failed. |
| `agent-smith:failed` | Pipeline failed. Error posted as a comment on the ticket. |

Filed phase tickets also carry the approved-set stamp, `phase-spec:approved` unless the tracker renames it (above).

The framework owns these; don't set them by hand. Lifecycle labels are removed before the `pipeline_from_label` match runs, so an `agent-smith:done` label doesn't accidentally re-trigger. Other `agent-smith:` labels, like your trigger labels, are left alone.

### Renaming them

If a board already uses one of these words, or a team wants its own, rename them per tracker with `label_names` (**Label names** in Config Studio):

```yaml
trackers:
  acme-jira:
    # ...
    label_names:
      waiting: needs-answer
      approved-set: spec-approved
```

The keys are `pending`, `enqueued`, `in-progress`, `waiting`, `done`, `shortfall`, `failed` and `approved-set`. Anything you leave out keeps its default. After a rename the framework writes the new name and still recognises the old one, so tickets labelled before the change keep working.

A renamed approved-set stamp does everything the default one does: it routes the ticket to `code`, holds it to its approved specification (a stamped ticket with nothing on its branch parks rather than deriving its own), and it is the word the ticket's notes and park messages name. The rename belongs to that tracker only. On another tracker the same word is an ordinary label, which may be one of its routing words.

Config Studio refuses to save a tracker or project where a framework label is the same word as a routing word: a `pipeline_from_label` key, or a project's tag resolution value. One word on a ticket would mean two things, and routing would quietly pick the framework's meaning. The refusal names the colliding word; rename one of the two.

### Labels are output, not state

The database is the system of record for a run; the label on the ticket is a best-effort projection of it. A label the tracker refuses to write is logged and doesn't block the run. Whether a ticket triggers again rests on its native status (`trigger_statuses`) plus the run lease — so the way to re-run a ticket is to move it back into a trigger status, not to fiddle with labels.

If you'd rather see real workflow states, a tracker can carry the lifecycle as native status transitions via a `lifecycle_status_names:` map on the tracker block (pending / enqueued / in-progress / done / failed → your status names). Labels stay as the always-available fallback.

## Parked tickets

When a run needs input from you (a too-thin ticket, an open question, an expectation to ratify, a specification that turned out wrong), it posts the question as a comment, parks the run, and labels the ticket `agent-smith:waiting`. A ticket too thin to start moves to your configured `needs_clarification_status`. The comment mentions the ticket's assignee, or the reporter when nobody is assigned, using the tracker's own mention syntax so that person gets notified. With neither, the comment says nobody was notified. The run checkpoints until you answer. See [Spec dialogue](../how-it-works/spec-dialogue.md#the-clarification-gate).

## Comment triggers

In addition to labels, you can trigger from a comment. Set a keyword on the project's trigger block:

```yaml
projects:
  todolist:
    # ...
    github_trigger:
      comment_keyword: "@agent-smith"
```

A ticket comment containing the keyword (case-insensitive) triggers the project's pipeline. No keyword configured means comment events are ignored — comments are noisy, so this is opt-in per project. (Comments are also how you answer a run's open questions when a ticket is parked — that path is always on and doesn't need the keyword.)

## Pull request labels

On GitHub and GitLab, putting the label `security-review` on a pull request or merge request starts a review of it. To use a word of your own as well, set `pr_trigger_label` on the project's `github_trigger` or `gitlab_trigger`:

```yaml
    github_trigger:
      pr_trigger_label: please-review
```

Your word is added to `security-review`, not swapped for it. See [Webhooks](webhooks.md).

## Don't trigger on labels you wrote

Trackers fire webhooks for every label add, including the lifecycle ones the framework writes back. The framework filters those out before the match step — adding `agent-smith:in-progress` doesn't re-trigger. But if you build your own automation on top, make sure it filters out the framework's labels likewise, or you'll have a loop.

## Next

- [Webhooks](webhooks.md) — how a label-add event reaches Agent Smith.
- [Polling](polling.md) — same matching logic, different transport.
- [CLI](cli.md) — bypass labels entirely, just say "fix this ticket".
