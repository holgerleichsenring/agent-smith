# Trigger: labels

Labels are how a ticket says "yes, run Agent Smith on this, with this pipeline". The mapping from label to pipeline lives in `pipeline_from_label` under each project's tracker block.

## The label-to-pipeline mapping

```yaml
projects:
  azuredevops-todolist:
    # ... agent, tracker, repos ...
    azuredevops_trigger:
      pipeline_from_label:
        agent-smith:init:               init-project
        agent-smith:bug:                fix-bug
        agent-smith:feature:            add-feature
        agent-smith:security-scan:      security-scan
        agent-smith:api-security-scan:  api-security-scan
```

Tag a work item / issue with `agent-smith:bug`, and the framework runs the `fix-bug` pipeline. Tag with `agent-smith:security-scan`, the `security-scan` pipeline. First match wins — labels are checked in declaration order.

The label values themselves are arbitrary; the convention is `agent-smith:{pipeline-name}` because it groups them together in the tracker's label UI and signals which labels are framework-controlled. You can rename them if your team prefers (`smith-fix`, `ai-fix`, whatever) — the YAML key is the label, the YAML value is the pipeline.

## Lifecycle labels

In addition to the trigger labels you set on the ticket, Agent Smith writes a lifecycle label to show where the run is:

| Label | Meaning |
|---|---|
| `agent-smith:pending` | New triggered ticket, eligible to be claimed. |
| `agent-smith:enqueued` | The ticket is claimed, the run is queued. |
| `agent-smith:in-progress` | The pipeline is running. |
| `agent-smith:done` | Pipeline finished successfully. |
| `agent-smith:failed` | Pipeline failed. Error posted as a comment on the ticket. |

The framework owns these — don't set them by hand. The trigger labels you set (`agent-smith:bug` etc.) are filtered out before the `pipeline_from_label` match runs, so a `agent-smith:done` label doesn't accidentally re-trigger.

Two things changed here over time and are worth knowing:

- **Labels are output, not state.** The database is the system of record for a run; the label on the ticket is a best-effort projection of it. Whether a ticket triggers again rests on its *native status* (`trigger_statuses`) plus the run lease — so the way to re-run a ticket is to move it back into a trigger status, not to fiddle with labels.
- **Native statuses instead of labels, if you want them.** A tracker can opt into carrying the lifecycle as real workflow transitions via a `lifecycle_status_names:` map on the tracker block (pending / enqueued / in-progress / done / failed → your status names). Labels stay as the always-available fallback carrier.

There's also a parking state: when a run needs input from you (a too-thin ticket, an open question), the ticket moves to your configured `needs_clarification_status` with the questions as a comment, and the run checkpoints until you answer. See [Spec dialogue](../how-it-works/spec-dialogue.md).

## Label format per tracker

Some trackers don't allow `:` in label names. The framework adapts:

| Tracker | Label format |
|---|---|
| Azure DevOps | `agent-smith:bug` (colon allowed) |
| Jira | `agent-smith-bug` (dashes, since Jira labels don't allow colons) |
| GitHub Issues | `agent-smith:bug` |
| GitLab Issues | `agent-smith:bug` |

For Jira, set `label_mode: true` on the tracker so the framework writes lifecycle labels with the dash format.

## What if no label matches

If a ticket comes in (via webhook or poll), enters one of `trigger_statuses`, but has no matching `pipeline_from_label` value, the framework ignores it. No run gets started, no label gets written. This is intentional — labels are how the human (or some upstream automation) explicitly opts a ticket in.

Two reasonable patterns for opting in:

- **Manual.** A developer tags `agent-smith:bug` when they want Agent Smith to fix it. Everything untagged stays human-only.
- **Default-on.** A workflow rule in the tracker auto-applies `agent-smith:bug` to every ticket of type Bug when it moves to Active. Now Agent Smith picks up every bug; the human can remove the label to opt out.

Both work. Default-on gives you the volume but you'll see more `agent-smith:failed` labels when the agent can't figure out a ticket. Manual is friendlier when you're still learning the agent's strengths.

## Comment triggers

In addition to labels, you can trigger from a comment. Set a keyword on the project's trigger block:

```yaml
projects:
  todolist:
    # ...
    github_trigger:
      comment_keyword: "@agent-smith"
```

A ticket comment containing the keyword triggers the project's pipeline. No keyword configured means comment events are ignored — comments are noisy, so this is opt-in per project. (Comments are also how you answer a run's open questions when a ticket is parked in `needs_clarification_status` — that path is always on and doesn't need the keyword.)

## Don't trigger on labels you wrote

Trackers fire webhooks for every label add, including the lifecycle ones the framework writes back. The framework filters those out before the match step — adding `agent-smith:in-progress` doesn't re-trigger. But if you build your own automation on top, make sure it filters out the `agent-smith:` prefix likewise, or you'll have a loop.

## Next

- [Webhooks](webhooks.md) — how a label-add event reaches Agent Smith.
- [Polling](polling.md) — same matching logic, different transport.
- [CLI](cli.md) — bypass labels entirely, just say "fix this ticket".
