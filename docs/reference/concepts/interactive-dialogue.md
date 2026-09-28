# Interactive Dialogue

Agent Smith conducts structured dialogues with the human during pipeline execution -- not just at the start or end, but exactly when clarification is needed. Every question and answer is logged in an auditable trail.

!!! note
    This page describes the underlying dialogue machinery (question types, transports, trail). The current behavior on top of it — checkpointing the run at the ask, resuming on the answer days later, the expectation ratification, the ticket clarification gate — is on [Expectations & durable dialogue](../../how-it-works/expectations.md) and [Spec dialogue](../../how-it-works/spec-dialogue.md).

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
  "parameters": {
    "question": "Question text to display to the human.",
    "context": "Optional context block shown alongside the question.",
    "choices": [{ "label": "Short choice label", "description": "Optional explanation" }],
    "recommended_index": "Optional 0-based index of the recommended choice"
  }
}
```

With choices it's a Choice question, without them a FreeText one.

The agent is instructed to ask sparingly. Good reasons to ask:

- Naming that requires domain knowledge (branch name, class name)
- Ambiguous acceptance criteria in the ticket
- Destructive operations (delete, rename, breaking change)
- Multiple equally valid architectural options

The agent should **not** ask about implementation details it can decide itself; it records those with `log_decision` instead.

## Channels

The same dialogue logic works across all channels:

### Slack and Teams

Slack renders each question type with Block Kit controls, Teams as an Adaptive Card — buttons for Confirmation, Approval and each choice, a free-text prompt for FreeText.

### Dashboard

A run waiting on a question shows it on the run's page, and the [Work it out](../../how-it-works/work-it-out.md) page shows a design conversation's questions and approvals inline.

### CLI

Interactive prompt with timeout. Confirmation shows `[Y]es / [N]o`, Choice shows numbered options, FreeText accepts direct input.

### PR Comments

Questions are posted as structured PR comments. The human responds with `/approve`, `/reject`, or `/approve Please rename the branch`. See [PR Comment Integration](../integrations/pr-comments.md) for details.

## Timeout Handling

A question asked inside a run doesn't hold compute while it waits. The run keeps its sandbox open for a short window expecting a fast answer, then checkpoints and parks; an answer that arrives later resumes it. Two settings in the top-level `dialogue:` block govern this:

```yaml
dialogue:
  hot_wait_seconds: 600            # how long the sandbox stays open for a fast answer
  approval_timeout_seconds: 259200 # how long the question stays answerable (three days)
```

The full loop is on [Expectations & durable dialogue](../../how-it-works/expectations.md). A design conversation's approval question waits fifteen minutes; see [Spec dialogue](../../how-it-works/spec-dialogue.md#confirming).

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
    +-- DurableDialogueTransport (server -- answers land in a durable inbox first,
    |                             then on the Redis stream for a waiting run)
    +-- ConsoleDialogueTransport (CLI -- interactive prompt)
```

`IDialogueTrail` accumulates all question-answer pairs in memory during the pipeline run and writes them to `result.md` at the end.
