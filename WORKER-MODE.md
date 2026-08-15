# Worker mode — running a whole ticket without a provider key

Worker mode swaps **who answers the model calls**. Everything else in a run is real:
real sandboxes, real repos, real gates, a real keystone, a real pull request. An external
agent CLI (Claude Code by default) answers each call instead of a provider API, so a
complete ticket can be exercised end to end at zero provider cost, as often as you like.

This exists because the only whole-system feedback this project had was a live run costing
roughly $10 and 90 minutes — and every one of those was cancelled before it proved
anything. Worker mode is that run, minus the money and the clock.

## What the worker is (and is not)

The worker enters as the **model**, not as an agent. Per call it receives what the
provider would have received — the system prompt, the whole conversation including
previous tool calls and their results, and the tool definitions with their JSON schemas —
and answers with assistant text or tool calls. agent-smith executes those tool calls.

That distinction is the whole point. Handing an agent CLI the ticket and a sandbox and
letting it use its own tools would bypass the machinery under test: the master loop, the
ledger, the reminders, the acceptance gate, the keystone. A green run would then say
nothing about agent-smith.

## Prerequisites

- The Claude Code CLI on PATH, authenticated headlessly (`claude setup-token`).
  Any binary works that reads a prompt on **stdin** and prints an answer on **stdout**.
- No provider key. Worker mode never proxies a credential and never impersonates a
  provider; the run spends nothing against an agent budget.

## Configuring a run

Worker mode is reachable **only** by declaring it on an agent. Nothing else — no missing
key, no provider outage, no default — can fall into it:

```yaml
agents:
  worker:
    type: external_worker        # the ONLY thing that selects worker mode
    model: sonnet                # passed through to the CLI as --model
    endpoint: /usr/local/bin/claude   # optional: the CLI binary (default: `claude` on PATH)
    network_timeout_seconds: 1800     # per-call wait (default 300 = 5 minutes)

projects:
  my-project:
    agent: worker
    ...
```

Then trigger the run exactly as usual (CLI, webhook, poller, dashboard). There is nothing
to start alongside it and nothing to poll: the run spawns the CLI once per model call and
waits for it. Runs are unattended.

### Environment overrides

| Variable | Effect | Default |
| --- | --- | --- |
| `AGENTSMITH_WORKER_CLI` | CLI binary; wins over `agent.endpoint` | `claude` |
| `AGENTSMITH_WORKER_CLI_ARGS` | extra CLI arguments, space-separated | – |
| `AGENTSMITH_WORKER_CWD` | working directory of the CLI process | the temp dir |

The working directory defaults to a neutral temp directory on purpose: the worker answers
a model call, so it must not pick up the project instructions or the source tree of the
repo the run is changing.

## What goes over the wire

There are no request/reply files. Each call renders one JSON envelope
(`protocol: agentsmith.worker/1`) into the CLI prompt on stdin:

```json
{
  "protocol": "agentsmith.worker/1",
  "request_id": "9f13c0a2b7de",
  "run_id": "2026-08-13T09-00-00-abcd",
  "step_index": 7,
  "role": "coding-master",
  "phase": "Implementation",
  "repo": "primary",
  "agent_type": "external_worker",
  "model": "sonnet",
  "messages": [
    { "role": "system",    "content": [ { "type": "text", "text": "..." } ] },
    { "role": "assistant", "content": [ { "type": "tool_call", "call_id": "call_1",
                                          "name": "write_file", "arguments": { "path": "src/A.cs" } } ] },
    { "role": "tool",      "content": [ { "type": "tool_result", "call_id": "call_1",
                                          "result": "written" } ] }
  ],
  "tools": [ { "name": "write_file", "description": "...", "input_schema": { "...": "..." } } ],
  "options": { "max_output_tokens": 8192, "not_rendered": ["Seed"] }
}
```

The CLI answers with one JSON object and nothing else:

```json
{ "text": "Adding the guard.",
  "tool_calls": [ { "name": "write_file", "arguments": { "path": "src/A.cs", "content": "..." } } ] }
```

`error` instead of an answer refuses the call explicitly.

`options.not_rendered` names every sampling option that was set but has no field in the
envelope — the payload declares its own gaps rather than leaving them to a comment.

This envelope is deliberately the payload p0166e's MCP worker mode will carry. When that
lands, the CLI subprocess is replaced by a JSON-RPC method; the payload does not change.

## When something goes wrong

Nothing degrades silently. A non-zero exit, a timeout, an empty answer, an unparseable
reply, or a call naming a tool that was never offered fails the call with
`ExternalWorkerCallException`, naming the request, the run, the step, the role and the
elapsed time — for example:

```
External worker call failed after 41.2s — request 9f13c0a2b7de
(run=2026-08-13T09-00-00-abcd step=7 role=coding-master phase=Implementation):
the worker's answer is not the agreed JSON object …
```

Per-call duration is logged for every call. Tokens cost nothing here; wall time and the
CLI's own session caps are the real limits, so watch the durations.

## Proving the wiring without a CLI

`ExternalWorkerTicketTests` in `tests/AgentSmith.PipelineHarness/Presets/` drives a whole
mechanical ticket through the same bridge with a scripted worker — one phase, the change
set applied at once, a green verdict, a pull request, no provider key, deterministic in
CI. It proves the **wiring**. Whether a live agent CLI can actually drive the loop to
green is what your local run proves; the bridge is identical, only the subprocess is
scripted.
