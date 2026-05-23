# Context Compaction

Long agentic runs (ProjectAnalyzer, AgenticExecute) accumulate the full conversation history each turn — every tool result, every assistant response. Without intervention this is **quadratic** in token cost: by iteration 20 the model re-processes 20× the prior tool output. Real numbers from a production trace: ProjectAnalyzer hit ~864k input tokens for a single .NET solution analysis (~$1.73 at gpt-4.1 input pricing).

Compaction summarizes the older prefix of the conversation when a threshold is crossed, keeping the recent tail verbatim. Same conceptual algorithm as p0008's Claude compactor, ported to OpenAI / Azure-OpenAI in p0114.

## Trigger

Compaction fires between rounds when **either**:

```
currentIterations >= CompactionConfig.ThresholdIterations
  OR
estimatedAccumulatedTokens >= CompactionConfig.MaxContextTokens
```

Boolean OR (no max(), no AND). Defaults: `ThresholdIterations=8`, `MaxContextTokens=80000`.

The estimate uses a `chars / 4` heuristic — accurate to ±5% for English text. It only feeds the trigger decision; the **post-compaction savings number** is the authoritative `usage.prompt_tokens` from the next API response.

## What's preserved, what's summarized

```
[ system prompt          ]   ← preserved verbatim (cache-stable)
[ initial user prompt    ]   ↘
[ assistant + tool_calls ]    │
[ tool result            ]    │  ← summarized into a single
[ assistant + tool_calls ]    │  [Context Summary] user message
[ tool result            ]    │
[ assistant text         ]   ↗
[ assistant + tool_calls ]   ↘
[ tool result            ]    │  ← TAIL: kept verbatim
[ assistant + tool_calls ]    │  (last 2 complete tool-call rounds)
[ tool result            ]   ↗
```

A "round" is one of:
- `AssistantChatMessage` with tool_calls + all `ToolChatMessage` responses to those `tool_call_id`s.
- A bare `AssistantChatMessage` with no tool_calls (terminal reply).

Walking backward, the boundary **never splits a round mid-pair** — the OpenAI API rejects payloads where a tool message references a `tool_call_id` without the matching assistant message in the same array. `ToolCallRoundIdentifier` enforces this invariant.

## Provider availability

| Provider | Compactor | Notes |
|---|---|---|
| Claude | `ClaudeContextCompactor` (p0008) | Uses Anthropic native `cache_control: ephemeral` — verifiable cache hits |
| OpenAI | `OpenAiContextCompactor` (p0114) | Uses chat-completions; opaque automatic prompt caching |
| Azure-OpenAI | `OpenAiContextCompactor` (p0114) | Same impl; Azure delegates via shared composition |
| Gemini | NoOp (placeholder) | Same long-loop quadratic cost as the others — **NOT** "doesn't need it". Follow-up phase. |
| Ollama | NoOp (placeholder) | Local model still re-processes the full history each turn. Follow-up phase. |

## Configuration

```yaml
agent:
  type: azure-openai
  model: gpt-4.1
  compaction:
    is_enabled: true                    # default true; disable for traceability/regulated runs
    threshold_iterations: 8             # fire once iterations reach this
    max_context_tokens: 80000           # fire once estimate reaches this
    keep_recent_iterations: 3           # legacy field; OpenAi compactor keeps 2 complete rounds
    summary_model: claude-haiku-4-5     # used by Claude compactor
    deployment_name: gpt-4o-mini-deployment  # NEW p0114: route summarizer to a smaller deployment
```

`deployment_name` is the most impactful operator knob: compaction is summarization, which doesn't need the primary model. Routing it to `gpt-4o-mini-deployment` (or equivalent) cuts the summarization-call cost by ~5× without degrading the summary quality.

## Failure handling

Summarizer failures are **non-fatal**:

- HTTP 429 (rate-limited summarizer deployment), 5xx, malformed response — caught at the compactor.
- Compactor returns `OpenAiCompactionResult(messages: original, Event: ForFailure(...))` — agentic loop continues with un-compacted history.
- Failure is logged at WARN with the original exception type + reason.

Pretending compaction succeeded with a broken summary is worse than running on the full history.

## Audit trail

Every `CompactionEvent` carries a `PromptHash` (8-char SHA-256 prefix of the resolved summarization-prompt). Operators correlating output regressions to prompt drift can diff hashes across runs. The prompt is loaded via `IPromptCatalog.Get("openai-context-compactor-system")` — operators can override it locally via `IPromptOverrideSource` (consistent with all other prompts in the codebase).

Log line for a successful compaction:

```
info  Compacted 24→3 messages; verified 18000 input tokens (saved est. 116000; summarizer cost 1200 tokens; prompt bae9264d)
```

The `verified` figure is the next API response's `usage.prompt_tokens` — ground truth, not estimate.

## Sequence assumption

The compaction point fires **synchronously between rounds**, never mid-tool-call. Every call site has an inline comment:

```csharp
// COMPACTION POINT — runs synchronously between rounds, after a complete
// assistant→tool-results round. Must NEVER fire while a tool_call is
// in-flight or unanswered. Future parallelization of the agentic loop
// must move this point or guard it explicitly.
```

If the agentic loop ever gets parallel-fanout for independent tool calls, this assumption breaks and the compaction point must be moved or guarded.
