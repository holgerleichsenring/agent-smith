# Context Compaction

A long agentic loop re-sends its whole conversation on every turn: every tool result, every assistant reply. Left alone, the input grows with each iteration and the run pays for the same tool output over and over, until it hits the model's context window and dies with an HTTP 400.

Compaction keeps that in check. Once the conversation gets too big, the older middle is folded into a running summary and the model keeps working on a smaller view of the same thread.

## One middleware for every provider

Compaction is a single provider-agnostic `CompactingChatClient` that sits in the chat-client chain below the function-invoking loop. Because the loop re-enters the chain on every tool iteration, the compactor sees each call and can reduce it mid-pass. Claude, OpenAI, Azure OpenAI, Gemini and Ollama all get the same behaviour; there is no per-provider compactor.

It runs in two places:

- The coding master's open loop always carries it (when `is_enabled` is true).
- Any other tool loop whose model role states a `context_window_tokens` gets it too, for example the scout sweep. A role without a stated window runs without compaction.

## Trigger

Compaction fires on token pressure only:

```
estimated tokens of the forwarded view >= max_context_tokens × max_context_tokens_trigger_ratio
```

Defaults are `max_context_tokens: 200000` and `max_context_tokens_trigger_ratio: 0.7`, so the first fold happens around 140k estimated tokens. When the role states a `context_window_tokens`, the threshold is the smaller of `max_context_tokens` and that window. A ratio of 0 or below switches the trigger off.

The estimate is a `chars / 4` count over message text and tool results. It only decides when to fold.

`threshold_iterations` is still parsed so old files load, but it does nothing. A non-default value logs a deprecation warning at startup.

## What the model sees after a fold

```
[ system prompt(s)       ]   pinned, verbatim
[ initial user message   ]   pinned, verbatim (ticket, conversation, attachments)
[ context summary        ]   the folded middle, extended incrementally
[ current state          ]   ledger + working state, re-rendered on every call
[ recent tail            ]   the last rounds, verbatim
```

- The initial user message is pinned. It carries the ticket and its attachments, and a paraphrase of it is how a model ends up re-deriving things the ticket spelled out.
- The tail keeps roughly the last `keep_recent_iterations` rounds (at least four messages) and never starts on an orphaned tool result, so every provider still gets a valid call/result transcript.
- The summary is framed as subordinate: where it conflicts, the ticket and the current-state block win.

After the first fold the view grows append-only with a byte-stable prefix. A new fold happens only when the view itself crosses the threshold again. That keeps provider prompt caches warm between folds instead of rewriting the prefix every turn.

## The summarizer

The summary call goes through the agent's `summarization` model role (see [AI providers](../../connect-your-stuff/ai-providers.md)), with up to 1024 output tokens per fold. `summary_model` and `deployment_name` under `compaction:` are still accepted but do not pick the summarizer; configure `models.summarization` instead.

A summarizer failure is not fatal. The compactor logs a warning ("Context compaction summarizer failed — forwarding the full history") and forwards the unreduced view.

## When folding is not enough

If a role states a `context_window_tokens` and the forwarded view still reaches 85% of it, the loop is finalized: tool calling is switched off for one turn and the model is told to answer from the evidence it already has. A shallow answer beats a context-length error.

## Configuration

```yaml
agents:
  claude-default:
    type: claude
    compaction:
      is_enabled: true                      # default true
      max_context_tokens: 200000            # default 200000
      max_context_tokens_trigger_ratio: 0.7 # default 0.7; 0 disables the trigger
      keep_recent_iterations: 3             # size of the verbatim tail
    models:
      summarization:
        model: claude-haiku-4-5-20251001
```

A successful fold logs one line:

```
info  Compaction fold #1: 58 -> 12 messages (watermark 50, folded 44 new)
```
