# Interactive Dialogue

Agent Smith conducts structured dialogues with the human during pipeline execution -- not just at the start or end, but exactly when clarification is needed. Every question and answer is logged in an auditable trail.

## Question Types

| Type | When | Example |
|------|------|---------|
| **Confirmation** | Yes/No decision | "Should I proceed with restructuring PaymentService?" |
| **Choice** | Selection from options | "Which database strategy: Repository Pattern or direct EF Core?" |
| **FreeText** | Open input needed | "What branch name should I use?" |
| **Approval** | Plan or change review | "Plan ready for approval (4 files, 1 new class)" |
| **Info** | Notification only | "Deployment started -- no action needed" |

## The `ask_human` Tool

During the agentic loop, the agent can ask questions via the `ask_human` tool:

```json
{
  "name": "ask_human",
  "input_schema": {
    "properties": {
      "question_type": { "enum": ["confirmation", "choice", "free_text", "approval"] },
      "text": { "description": "The question to ask" },
      "context": { "description": "Why are you asking? Max 300 chars" },
      "choices": { "description": "Only for type=choice" },
      "default_answer": { "description": "Used on timeout" }
    }
  }
}
```

The agent is instructed to ask sparingly. Good reasons to ask:

- Naming that requires domain knowledge (branch name, class name)
- Ambiguous acceptance criteria in the ticket
- Destructive operations (delete, rename, breaking change)
- Multiple equally valid architectural options

The agent should **not** ask about implementation details it can decide itself, and should prefer logging a decision in `decisions.md` over asking.

## Channels

The same dialogue logic works across all channels:

### Slack

Block Kit renders each question type with appropriate controls -- buttons for Confirmation, numbered options for Choice, free-text prompt for FreeText. Approval includes optional comment via next message.

### CLI

Interactive prompt with timeout. Confirmation shows `[Y]es / [N]o`, Choice shows numbered options, FreeText accepts direct input.

### PR Comments

Questions are posted as structured PR comments. The human responds with `/approve`, `/reject`, or `/approve Please rename the branch`. See [PR Comment Integration](../integrations/pr-comments.md) for details.

## Timeout Handling

Every question has a configurable timeout (default: 5 minutes for Slack/CLI, 24 hours for PR comments). When the timeout expires, the `default_answer` is used and the pipeline continues.

```yaml
agent:
  dialogue:
    timeout_seconds: 300
    default_on_timeout: yes
```

## Dialogue Trail

Every question and answer is accumulated in `PipelineContext` and written to `result.md` as an audit log:

```markdown
## Dialogue Trail

| Time | Question | Type | Answer | By | Timeout? |
|------|----------|------|--------|-----|----------|
| 14:03:12 | Should I proceed? | Confirmation | Yes | @holger | No |
| 14:07:44 | Which branch name? | FreeText | feature/pay-refactor | @holger | No |
| 14:22:01 | Tests failed -- proceed? | Confirmation | Yes (default) | timeout | Yes |

**3 questions, 2 human answers, 1 timeout**
```

The trail is never lost. It appears in the PR alongside the code changes, so reviewers see exactly what the agent asked and what the human decided.

## Architecture

The dialogue system is transport-agnostic:

```
AgenticLoop / Pipeline Handler
    |
    v
IDialogueTransport          (publishes questions, waits for answers)
    |
    +-- RedisDialogueTransport   (Slack, PR comments -- via Redis streams)
    +-- ConsoleDialogueTransport (CLI -- interactive prompt)
```

`IDialogueTrail` accumulates all question-answer pairs in memory during the pipeline run and writes them to `result.md` at the end.
