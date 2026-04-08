# Phase 58: Interactive Dialogue — Ping-Pong Across All Channels

## Goal

The agent conducts a structured, auditable dialogue with the human —
during execution, not just at the start or end. The result is never
"works / doesn't work" but always a clear question-answer exchange with
a complete audit trail.

Applies to: **Slack, CLI, PR comment** — same logic, different transports.
Teams support is deferred to **p58b**.

---

## The Problem Today

```
Today:
  Agent runs → [occasional AskQuestion with Yes/No buttons] → done

What's needed:
  Agent runs → question with context → human answers structured →
  agent integrates answer → continues → next question if needed →
  complete trail of all Q&As in result.md
```

`AskQuestion` already exists in `IPlatformAdapter` — but:
- Yes/No only, no free-text answers
- No question type concept (decision vs. clarification vs. approval)
- No structured trail
- CLI: blocks synchronously, no timeout handling
- PR comment: not available

---

## Core Concept: DialogQuestion

Every question has a **type**. The type determines which answer options are
offered, how the answer is processed in the AgenticLoop, and what appears
in the trail.

```csharp
// AgentSmith.Contracts/Dialogue/DialogQuestion.cs

public enum QuestionType
{
    /// Yes/No decision — classic approval case
    Confirmation,

    /// Selection from predefined options (1-N)
    Choice,

    /// Free text — the human provides an instruction or explanation
    FreeText,

    /// Approval with optional comment (Approve / Reject + free text)
    Approval,

    /// Informational only — no input expected, just acknowledge
    Info,
}

public sealed record DialogQuestion(
    string QuestionId,           // GUID, unique per question
    QuestionType Type,
    string Text,                 // The actual question
    string? Context,             // Why is the agent asking? (max 300 chars)
    IReadOnlyList<string>? Choices,  // Only for Type=Choice
    string? DefaultAnswer,       // Used after timeout
    TimeSpan Timeout);           // Default: 5 minutes

public sealed record DialogAnswer(
    string QuestionId,
    string Answer,               // "yes"/"no", option text, free text
    string? Comment,             // Optional comment for Approval
    DateTimeOffset AnsweredAt,
    string AnsweredBy);          // userId of the human
```

---

## IPlatformAdapter — Extension

```csharp
// AgentSmith.Contracts/Adapters/IPlatformAdapter.cs

public interface IPlatformAdapter
{
    // Existing (remains):
    Task SendMessageAsync(string channelId, string text, CancellationToken ct);
    Task SendProgressAsync(string channelId, int step, int total, string commandName, CancellationToken ct);
    Task SendDoneAsync(string channelId, string summary, string? prUrl, CancellationToken ct);
    Task SendErrorAsync(string channelId, string text, CancellationToken ct);
    Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken ct);

    // EXISTING — marked [Obsolete], migrated in p58b:
    [Obsolete("Use AskTypedQuestionAsync instead")]
    Task<...> AskQuestionAsync(...);

    // NEW:
    /// Asks a typed question. Blocks until answer or timeout.
    /// Returns null on timeout (agent uses DefaultAnswer).
    Task<DialogAnswer?> AskTypedQuestionAsync(
        string channelId,
        DialogQuestion question,
        CancellationToken ct);

    /// Sends an informational message with acknowledge button (no waiting).
    Task SendInfoAsync(string channelId, string title, string text, CancellationToken ct);
}
```

---

## Transport Implementations

### Slack: SlackAdapter

**Confirmation (Yes/No):**
```
❓ *Should I proceed?*
> The existing class `PaymentService` will be restructured.
  This affects 3 dependent services.

[✅ Yes, proceed]  [❌ No, cancel]
```

**Choice:**
```
❓ *Which database strategy should be used?*
> The current code uses Repository Pattern.
  Both options are compatible.

[1️⃣ Keep Repository Pattern]
[2️⃣ Switch to direct EF Core]
[3️⃣ Let the agent decide]
```

**Approval (with free text):**
```
❓ *Plan ready for approval*
> 4 files will be changed, 1 new class.
  Estimated complexity: medium.

[✅ Approve]  [❌ Reject]
💬 _Comment optional — just type as next message_
```

**FreeText:**
```
❓ *What branch name should I use?*
> Default pattern would be: `feature/payment-refactor-#123`

💬 _Reply with your desired name_
```

Implementation: Slack Block Kit, `action_id` contains `{questionId}:{answer}`.
Free-text answers: next message in channel with `questionId` as state in Redis.

### CLI: CliAdapter

**Confirmation:**
```
┌─────────────────────────────────────────────────────┐
│ ❓  Should I proceed?                                │
│                                                     │
│     The existing class PaymentService will be       │
│     restructured. This affects 3 services.          │
│                                                     │
│     [Y]es proceed  /  [N]o cancel                   │
│     (Timeout: 5 min — Default: Yes)                 │
└─────────────────────────────────────────────────────┘
> _
```

**Choice:**
```
┌─────────────────────────────────────────────────────┐
│ ❓  Which database strategy?                         │
│                                                     │
│     [1] Keep Repository Pattern                     │
│     [2] Switch to direct EF Core                    │
│     [3] Let the agent decide                        │
│                                                     │
│     Input: _
```

**FreeText:**
```
┌─────────────────────────────────────────────────────┐
│ ❓  What branch name to use?                         │
│     Default: feature/payment-refactor-#123          │
└─────────────────────────────────────────────────────┘
> _
```

Timeout on CLI: after 5 minutes without input, `DefaultAnswer` is used,
with hint: `⏱ Timeout — using default: "Yes"`

### PR Comment: PrCommentAdapter (NEW)

After pipeline end or at a blocking step, a structured PR comment is posted.
The human answers with `/approve`, `/reject`, or free text.

```markdown
## 🤖 Agent Smith — Question

**Should I proceed?**

> The existing class `PaymentService` will be restructured.
> This affects 3 dependent services.

**Respond:**
- `/approve` — Yes, proceed
- `/reject` — No, cancel
- `/approve Please rename branch to feature/xyz` — with comment

_Timeout: 24 hours. After that: Default (Yes)_
```

Webhook handler reads PR comments and forwards structured answers
via Redis — same protocol as Slack.

---

## Dialogue Transport: Redis Protocol

The existing `job:{id}:in` / `job:{id}:out` stream schema is extended:

```
# Agent → Dispatcher (question)
XADD job:{id}:out * type question questionId {guid} questionType Confirmation
                    text "Should I proceed?" context "PaymentService..."
                    choices "" defaultAnswer "yes" timeoutSeconds 300

# Dispatcher → Agent (answer)
XADD job:{id}:in * type answer questionId {guid} answer "yes"
                   comment "" answeredBy "U12345" answeredAt "2026-04-02T..."
```

`IDialogueTransport` abstracts the transport:

```csharp
public interface IDialogueTransport
{
    Task PublishQuestionAsync(string jobId, DialogQuestion question, CancellationToken ct);
    Task<DialogAnswer?> WaitForAnswerAsync(string jobId, string questionId,
        TimeSpan timeout, CancellationToken ct);
    Task PublishAnswerAsync(string jobId, DialogAnswer answer, CancellationToken ct);
}
```

---

## DialogueHandler — New Command

A new pipeline command `AskCommand` can be inserted anywhere in the pipeline
— from Triage, from SkillRounds, from AgenticExecute.

```csharp
// Parameterized: "AskCommand:confirmation:Should I proceed?"
// Or dynamically via InsertNext from another handler

public sealed class AskCommandHandler(
    IPlatformAdapter adapter,
    IDialogueTransport transport,
    IDialogueTrail trail,
    ILogger<AskCommandHandler> logger)
    : ICommandHandler<AskContext>
{
    public async Task<CommandResult> ExecuteAsync(AskContext context, CancellationToken ct)
    {
        var question = context.Question;

        // Ask question
        var answer = await adapter.AskTypedQuestionAsync(
            context.Pipeline.ChannelId, question, ct);

        // Timeout handling
        if (answer is null)
        {
            var defaultUsed = new DialogAnswer(question.QuestionId,
                question.DefaultAnswer ?? "yes", null, DateTimeOffset.UtcNow, "timeout");
            await trail.RecordAsync(question, defaultUsed);
            context.Pipeline.Set($"answer:{question.QuestionId}", defaultUsed.Answer);
            return CommandResult.Ok($"Timeout — default used: {defaultUsed.Answer}");
        }

        // Answer into PipelineContext
        await trail.RecordAsync(question, answer);
        context.Pipeline.Set($"answer:{question.QuestionId}", answer.Answer);

        // Rejection leads to pipeline abort
        if (answer.Answer == "no" || answer.Answer == "reject")
            return CommandResult.Fail($"Cancelled by {answer.AnsweredBy}: {answer.Comment}");

        return CommandResult.Ok($"Answer: {answer.Answer}");
    }
}
```

---

## Dialogue Trail — Audit Log

Every question + answer is accumulated in `PipelineContext` and written
to `result.md`. Never lost, always visible.

```csharp
public interface IDialogueTrail
{
    Task RecordAsync(DialogQuestion question, DialogAnswer answer);
    IReadOnlyList<DialogTrailEntry> GetAll();
}
```

**result.md — new section:**

```markdown
## Dialogue Trail

| Time | Question | Type | Answer | By | Timeout? |
|------|----------|------|--------|-----|----------|
| 14:03:12 | Should I proceed? | Confirmation | ✅ Yes | @holger | No |
| 14:07:44 | Which branch name? | FreeText | feature/pay-refactor | @holger | No |
| 14:22:01 | Tests failed — proceed? | Confirmation | ✅ Yes (default) | timeout | Yes |

**3 questions, 2 human answers, 1 timeout**
```

---

## AgenticLoop — AskQuestion from the Agent

The agent can ask questions during execution — via tool:

```json
{
  "name": "ask_human",
  "description": "Ask the human a question when clarification is needed. Use sparingly.",
  "input_schema": {
    "type": "object",
    "properties": {
      "question_type": { "type": "string", "enum": ["confirmation", "choice", "free_text", "approval"] },
      "text": { "type": "string", "description": "The question to ask" },
      "context": { "type": "string", "description": "Why are you asking? Max 300 chars" },
      "choices": { "type": "array", "items": { "type": "string" }, "description": "Only for type=choice" },
      "default_answer": { "type": "string", "description": "Used on timeout" }
    },
    "required": ["question_type", "text", "context", "default_answer"]
  }
}
```

**When should the agent ask?** System prompt rule:

```
## Human Interaction Rules
- Ask ONLY when genuinely ambiguous and the wrong choice would cause significant rework.
- Never ask about implementation details you can decide yourself.
- Never ask more than once per pipeline stage.
- Always provide a sensible default_answer so the pipeline can continue on timeout.
- Prefer logging a decision in log_decision over asking the human.

Good reasons to ask:
  - Naming that requires domain knowledge (branch name, class name)
  - Ambiguous acceptance criteria in the ticket
  - Destructive operations (delete, rename, breaking change)
  - Multiple equally valid architectural options

Bad reasons to ask:
  - "Should I add tests?" (always yes)
  - "Which file should I create?" (you decide)
  - "Is this approach okay?" (decide and log in decisions.md)
```

---

## Steps

### Step 1: Contracts — DialogQuestion, DialogAnswer, IDialogueTransport, IDialogueTrail
`AgentSmith.Contracts/Dialogue/`

### Step 2: Redis Transport
`AgentSmith.Infrastructure/Dialogue/RedisDialogueTransport.cs`
Extends existing `job:{id}:in` / `job:{id}:out` schema.

### Step 3: CLI Adapter — AskTypedQuestionAsync
`AgentSmith.Host/Adapters/CliAdapter.cs` — interactive + timeout support.

### Step 4: Slack Adapter — Extension
`SlackAdapter.AskTypedQuestionAsync` — Block Kit for all 5 QuestionTypes.
Free-text state in Redis with 10-minute TTL.

### Step 5: PR Comment Adapter — new
`AgentSmith.Infrastructure/Adapters/PrCommentAdapter.cs`
GitHub comment as question transport (GitHub only).
Webhook handler for `/approve` `/reject` answers.

### Step 6: `ask_human` Tool in AgenticLoop
`ToolExecutor` + `ToolDefinitions` — new tool, system prompt rules.

### Step 7: AskCommandHandler + IDialogueTrail
Pipeline command + trail accumulation + result.md section.

### Step 8: Tests + Verify

---

## Definition of Done

- [x] `DialogQuestion` / `DialogAnswer` / `QuestionType` in Contracts
- [x] `IDialogueTransport` + `RedisDialogueTransport` implemented
- [x] `IDialogueTrail` + `InMemoryDialogueTrail` implemented
- [x] CLI: all 5 QuestionTypes interactive with timeout (`ConsoleDialogueTransport`)
- [x] Slack: all 5 QuestionTypes as Block Kit
- [x] PR comment: question + `/approve` `/reject` webhook handler (GitHub only)
- [x] `ask_human` tool in AgenticLoop + system prompt rules
- [x] `AskCommandHandler` usable in pipeline
- [x] Dialogue trail in `result.md` always present
- [x] Existing `AskQuestion` marked `[Obsolete]`
- [x] All existing tests green (no regression)
- [x] `dotnet build` zero warnings

---

## Dependencies

```
Step 1 (Contracts)
    ├── Step 2 (Redis Transport)
    │       ├── Step 4 (Slack extension)
    │       └── Step 5 (PR Comment — new, GitHub only)
    ├── Step 3 (CLI)
    ├── Step 6 (ask_human tool)
    └── Step 7 (AskCommandHandler + Trail)
            └── Step 8 (Tests)
```

Steps 3, 4, 5 are parallelizable after Steps 1+2.

---

## Out of Scope (deferred)

| Feature | Deferred to |
|---|---|
| Teams adapter (Adaptive Cards, JWT, Bot Service) | p58b |
| Multi-repo dialogue integration | p23 |
| Removal of deprecated `AskQuestion` | p58b |
