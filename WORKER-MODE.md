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
    worker_structured_result: true    # optional: ask the CLI for its structured result

projects:
  my-project:
    agent: worker
    ...
```

Then trigger the run exactly as usual (CLI, webhook, poller, dashboard). There is nothing
to start alongside it and nothing to poll: the run spawns the CLI once per model call and
waits for it. Runs are unattended.

### What the call cost

`worker_structured_result` asks the CLI for its own structured result
(`--output-format stream-json`, whose last line is the result) instead of bare text. With it, a worker call reports the tokens
it used and the USD figure the CLI itself names, and the run's `result.md` carries a
`worker_cli:` block under `cost:`.

It is **opt-in per agent**, and unset means: on for the default `claude` binary, off for
anything else. That is the whole point of the knob — this document promises that any
binary reading a prompt on stdin and printing an answer works, and an unknown binary
handed an unknown flag exits non-zero, which the bridge treats as a dead call.

Two things the reported figure is not:

- **It is not money charged here.** No money is charged against an agent budget on this
  transport. In `result.md` the figure is carried in its own channel with its own label,
  never added to `total_usd` and never reported as an unpriced model — pricing it from the
  model table would invent money the run never paid.
- **It is not comparable to a provider call.** A trivial probe reported a third of a cent
  on twenty-six thousand cache-creation tokens: the CLI's own system prompt and tool
  schemas, charged per call, not this run's context.

Both arms of the per-pipeline budget cap count worker calls: the token arm their volume, and
since 2026-11-17-b34ea the USD arm the figure the CLI reported, summed over the run's recorded
calls. A worker run over its `pipeline_cost_cap` stops like a provider run.

The run's RECORDED cost — the figure on the run, the ticket and the delivery pages, and the
one a series budget is read against — is the sum of its recorded calls, and a worker call
records the figure its CLI reported (2026-11-17-166ab). It is what the work would cost at the
CLI's own prices, labelled as the CLI's on every call row.

The answer is unwrapped at the transport, before anything else reads it. That matters more
than it sounds: a structured result is never empty, so the retry-on-silence check and the
empty-turn path would both stop firing if anything downstream still read raw stdout — and
an empty answer would become a failed run where it used to be a nudge.

### Environment overrides

| Variable | Effect | Default |
| --- | --- | --- |
| `AGENTSMITH_WORKER_CLI` | CLI binary; wins over `agent.endpoint` | `claude` |
| `AGENTSMITH_WORKER_CLI_ARGS` | extra CLI arguments, space-separated | – |
| `AGENTSMITH_WORKER_CWD` | working directory of the CLI process | `<temp>/agentsmith-worker` |

The working directory defaults to its own empty directory under the temp directory on
purpose: the worker answers a model call, so it must not pick up the project instructions,
the source tree of the repo the run is changing, or the sandbox clones that live in the
temp directory itself.

A Claude Code CLI — the default `claude` binary, or any binary an agent sets
`worker_structured_result: true` for — is also started with `--tools ""`,
`--strict-mcp-config` and `--no-session-persistence`: no built-in file tools, no MCP
servers, no session saved per call. Measured on CLI 2.1.285, that took a one-word call from
32,852 input tokens to 953 (2026-11-17-166ac). It also runs with
`CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC=1`: without it the CLI sent every call's whole
input, uncached, to a second model (`claude-haiku-4-5` in the result's `modelUsage`), 8 % of
a replay's cost (2026-11-17-cb32b).

## What goes over the wire

There are no request/reply files. Each call renders one JSON envelope
(`protocol: agentsmith.worker/1`) for the CLI, shown here whole:

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

The CLI receives the envelope in two parts (2026-11-17-166ae). `CONVERSATION HEAD` holds
the instructions, `protocol`, `agent_type`, `model`, the conversation's leading system
messages (as `system`) and `tools` — the same on every call of a conversation. `REQUEST`
holds the call's identity, the rest of the `messages` and `options`. A Claude Code CLI
reads the head from `--system-prompt-file` (a file because Linux caps one argument at
128 KB), which its prompt cache serves on the next call; only the request goes on stdin.
Any other binary reads both parts on stdin, head first. Measured on the replay of
2026-11-17: 164 of 172 calls read only the CLI's own prompt from cache and wrote the
whole conversation as new cache — a stdin text is one content block, and no cache entry
ever matches a prefix inside one.

With the structured result on, the request goes in as content blocks (2026-11-17-d6f5):
one stream-json user message (`--input-format stream-json`) whose first block is the
call's identity and options — without `request_id` and `created_utc`, which change on
every call — followed by one block per message. The CLI marks the last block for caching
and a cached prefix is read where a block ends, so the blocks a call ended with are read
by the next call of the conversation instead of written again. The CLI also writes its
session id into its own prompt ahead of the request, so every call of a conversation
passes the same `--session-id`, a UUID derived from the head and the first two blocks.
Measured on the replay of 2026-11-17 before this change: 99 % of the cache writes were
conversation history, 183 reviewer calls averaging 1,923 tokens read against 28,813
written. A five-call probe after it reads the whole previous call and writes about one
tool round per call.

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

Per-call duration is logged for every call. Wall time and the CLI's own session caps are
the real limits here, so watch the durations — and with `worker_structured_result`, a CLI
that reports its own error (out of turns, an error during execution) fails the call with
its stated reason even though it exited zero.

## Proving the wiring without a CLI

`ExternalWorkerTicketTests` in `tests/AgentSmith.PipelineHarness/Presets/` drives a whole
mechanical ticket through the same bridge with a scripted worker — one spec, the change
set applied at once, a green verdict, a pull request, no provider key, deterministic in
CI. It proves the **wiring**. Whether a live agent CLI can actually drive the loop to
green is what your local run proves; the bridge is identical, only the subprocess is
scripted.
