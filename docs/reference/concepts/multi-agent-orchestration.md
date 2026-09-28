# Multi-agent orchestration

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited under **Configuration → Limits** in the [Config studio](../../configure-it/config-studio.md); the CLI reads them from `agentsmith.yml`. See [Where configuration lives](../../configure-it/index.md).

Every model-driven pipeline in Agent Smith runs one agent at its centre: a master. The master is a skill with `role: master` in the [skills catalog](../configuration/skills.md), and it runs an open tool loop at the pipeline's `AgenticMaster` step. When the work splits into independent pieces, the master can hand them to child agents that run in parallel and report back. That is the whole orchestration model: one master, any number of children up to a budget, one level deep.

## Which master a pipeline runs

The pipeline's name picks the master:

| Pipeline | Master |
|---|---|
| `code` | `coding-agent-master`, once per derived phase (each phase runs master, then `VerifyPhase`; see [Methodology](../../how-it-works/methodology.md)) |
| `security-scan` | `security-master` |
| `api-security-scan` | `api-security-master` |
| `pr-review` | `pr-review-master` |
| `legal-analysis` | `legal-analyst-master` |
| `mad-discussion` | `mad-discussion-master` |
| `spec-dialog` | `design-partner-master` (see [Spec dialogue](../../how-it-works/spec-dialogue.md)) |

Any pipeline name not in this table resolves to `coding-agent-master`.

The master's `output_schema` decides its tool surface. A master that declares `output_schema: observation` (the scan and review masters) gets a read-only surface and emits findings. A master without it is a coding master: it can read and write files in the run's sandboxes, gets `update_progress` for its progress ledger and `ensure_repo_sandbox` to bring in a repository the scoping step left out. The design partner gets a read-only surface of its own.

## Spawning children

Every master surface carries two extra tools as long as `limits.max_sub_agents_per_run` is above zero (for the design partner, `limits.max_sub_agents_per_dialog_turn`):

- `spawn_agents` takes a list of tasks and runs them in parallel.
- `read_sub_agent_observations` returns one child's full final answer.

Each task the master passes to `spawn_agents` carries:

| Field | Meaning |
|---|---|
| `name` | A descriptive role name for the child. Generic names (`worker`, `helper`, `agent`, `agent1`, `sub2`, `child3` and similar) are refused before any model call. |
| `activity` | A one-line description of what the child is doing. |
| `task_description` | The work itself. |
| `inherited_context` | `pipeline_goal`, `prior_context_slice` and an optional `system_prompt_block`, so the child does not have to rediscover the run's goal. |
| `output_hint` | Optional: the shape the master wants back. |

The child's prompt is built from exactly these fields. It does not see the master's conversation.

## What a child may do

A child runs its own tool loop with the master's base surface:

- It works in the same sandboxes as the master, so its reads and writes land in the same working copy.
- A child of a scan master is read-only, like its parent. A child of a coding master can read and write.
- A child never gets `spawn_agents` or `read_sub_agent_observations`, so it cannot spawn grandchildren. It also never gets `update_progress` or `ensure_repo_sandbox`; the ledger and the repository scope stay with the master.
- A child of a design turn additionally loses `ask_human` and `remember`. A conversation holds one pending question at a time, and the read-only source scope refuses a memory write.

A child has no budget fence and no ledger reminders. Its only bound is its iteration ceiling: `max_sub_agent_loop_iterations` on the agent the run uses (default 100), or `limits.max_dialog_sub_agent_loop_iterations` (default 20) for a child of a design turn.

## How results come back

`spawn_agents` waits until every child has finished and returns one row per task, in the order the master listed them:

| Field | Meaning |
|---|---|
| `status` | `Succeeded` or `Failed`. |
| `sub_agent_id` | The id to pass to `read_sub_agent_observations`. |
| `name` | The child's name. |
| `observations_count`, `tool_calls` | How much the child did. |
| `cost_usd` | What the child spent. |
| `reason` | Set on refusals: `invalid_name` or `budget_exhausted`. |

The row carries counts, not content. To use what a child found, the master calls `read_sub_agent_observations` with the child's `sub_agent_id` and gets the child's final answer back as text.

A failed child does not stop its siblings; it comes back as a `Failed` row and the master decides what to do next. Each call to `spawn_agents` is also written to the run's decision log with every child's name, activity, status and cost, and the failure reason for a child that failed.

## Limits

```yaml
limits:
  max_concurrent_sub_agents: 4               # children in flight at once
  max_sub_agents_per_run: 20                 # children across all spawn_agents calls in one run; 0 removes the tools
  max_sub_agents_per_dialog_turn: 4          # the same count for one design turn; 0 removes the tools there
  max_dialog_sub_agent_loop_iterations: 20   # iteration ceiling for a design turn's child

agents:
  claude-default:
    max_sub_agent_loop_iterations: 100       # iteration ceiling for every other child
```

The per-run count is a budget that `spawn_agents` reserves from. When a call asks for more children than are left, the ones that fit run and the rest come back as `Failed` with `reason: budget_exhausted`, without a model call. A design turn builds a budget of its own from `max_sub_agents_per_dialog_turn`, so one conversation turn cannot open twenty children.

Children's model usage is added to the run's cost tracker, so it counts against the run's [cost cap](../configuration/pipeline-cost-cap.md) and shows up in the run's total.

## In the dashboard

A run's activity feed shows a "Sub-agent spawn" row per child with its name and activity, and a "Sub-agent done" row with its status, counts and cost.
