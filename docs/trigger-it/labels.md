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

In addition to the trigger labels you set on the ticket, Agent Smith adds a lifecycle label to track where the run is:

| Label | Meaning |
|---|---|
| `agent-smith:pending` | Default for any new triggered ticket. Eligible to be claimed. |
| `agent-smith:enqueued` | A receiver claimed the ticket and pushed a `PipelineRequest` onto the Redis job queue. |
| `agent-smith:in-progress` | A consumer pulled the request, the pipeline is running. Heartbeat in Redis. |
| `agent-smith:done` | Pipeline finished successfully. |
| `agent-smith:failed` | Pipeline failed. Error posted as a comment on the ticket. |

The framework owns these — don't set them by hand. The trigger labels you set (`agent-smith:bug` etc.) are filtered out before the `pipeline_from_label` match runs, so a `agent-smith:done` label doesn't accidentally re-trigger.

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

## Comment commands

In addition to labels, you can trigger from a comment. Drop this comment on a ticket:

```
/agent-smith fix
```

The framework parses the slash-prefix line and runs the named pipeline if it's allowed under `pipeline_from_label` for this project. The list of allowed commands per project is the values of `pipeline_from_label` — `fix-bug`, `add-feature`, `security-scan`, etc.

Comment commands are off by default. To enable, add to the tracker block:

```yaml
trackers:
  acme-issues:
    # ...
    comment_commands_enabled: true
```

## Don't trigger on labels you wrote

Trackers fire webhooks for every label add, including the lifecycle ones the framework writes back. The framework filters those out before the match step — adding `agent-smith:in-progress` doesn't re-trigger. But if you build your own automation on top, make sure it filters out the `agent-smith:` prefix likewise, or you'll have a loop.

## Next

- [Webhooks](webhooks.md) — how a label-add event reaches Agent Smith.
- [Polling](polling.md) — same matching logic, different transport.
- [CLI](cli.md) — bypass labels entirely, just say "fix this ticket".
