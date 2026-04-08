# Phase 59: PR Comment as Input Type — Start & Dialogue via Webhook

## Goal

PR comments become full-fledged inputs for Agent Smith.
Two scenarios, one shared foundation:

**Scenario A — New job via comment:**
Reviewer writes `/agent-smith fix this` in a PR comment.
→ New pipeline job starts with the PR as context.

**Scenario B — Dialogue answer via comment (Phase 58):**
Agent asked in the PR: "Should I proceed?"
→ Reviewer answers `/approve` or `/reject [reason]`.
→ Running job picks up the answer and continues.

Both scenarios use the same webhook entry point,
the same signature verification, the same Redis infrastructure.

**MVP scope: GitHub only.** GitLab → p59b. Azure DevOps → p59c.

---

## What Already Exists

| Component | Status |
|---|---|
| `IWebhookHandler` dispatch pattern | p43e — must be implemented first |
| `WebhookListener` (p14) | Implemented — GitHub Issues only, label `agent-smith` |
| Signature verification | Missing completely |
| Redis `job:{id}:in` for answers | Implemented (p18) |
| `ConversationStateManager` | Implemented (p18) |
| `IPlatformAdapter.AskTypedQuestionAsync` | Phase 58 |

Phase 59 requires p43e (Webhook Dispatch) and p58 Steps 1–2 (Redis Transport)
for Scenario B.

---

## Architecture: Shared Entry Point

```
GitHub
    │
    │  POST /webhook  (HTTP, signed)
    ▼
WebhookListener (thin HTTP server, p43e-refactored)
    │
    │  CanHandle(platform, eventType)?
    ▼
IWebhookHandler Dispatch
    │
    └── GitHubPrCommentWebhookHandler
          │
          │  ParseCommentIntent(body) → CommentIntent
          ▼
    CommentIntentRouter
          │
          ├── IsDialogueAnswer?  →  Redis job:{id}:in XADD (Scenario B)
          └── IsNewJobCommand?   →  Redis job-queue XADD  (Scenario A)
```

---

## CommentIntent — The Central Model

```csharp
// AgentSmith.Contracts/Webhooks/CommentIntent.cs

public enum CommentIntentType
{
    /// /agent-smith <pipeline> [optional parameters]
    NewJob,

    /// /approve [optional comment]
    DialogueApprove,

    /// /reject [reason]
    DialogueReject,

    /// /agent-smith help
    Help,

    /// No known command — ignore
    Unknown,
}

public sealed record CommentIntent(
    CommentIntentType Type,
    string Platform,          // "github" (MVP), "gitlab", "azdo" (future)
    string RepoFullName,      // "owner/repo"
    string PrIdentifier,      // PR number
    string CommentId,         // For reply comment
    string AuthorLogin,       // Who commented
    string? Pipeline,         // Only for NewJob: "fix-bug", "security-scan", etc.
    string? RawArguments,     // Everything after the command
    string? DialogueComment,  // Optional text for approve/reject
    string CommentBody);      // Original comment (for logging)
```

---

## Command Syntax

All commands start with `/agent-smith` or the short alias `/as`.

### Scenario A — New Job

```
/agent-smith fix                         → fix-bug pipeline for this PR
/agent-smith fix #123 in my-api          → fix-bug for specific ticket
/agent-smith security-scan               → security-scan for this PR
/agent-smith review                      → PR review pipeline (p25)
/agent-smith help                        → Responds with available commands
```

Without parameters (`/agent-smith fix`), the handler passes the PR context
directly as ticket reference — the agent reads PR description and comments
as context.

### Scenario B — Dialogue Answer

```
/approve                                 → Confirmation (yes)
/approve Please rename the branch        → Confirmation with comment
/reject                                  → Rejection (no)
/reject The naming is wrong              → Rejection with reason
```

Short forms are supported. Case-insensitive.

---

## CommentIntentParser

```csharp
// AgentSmith.Application/Webhooks/CommentIntentParser.cs

public static class CommentIntentParser
{
    // Regex patterns (case-insensitive)
    private static readonly Regex AgentSmithCmd =
        new(@"^/(?:agent-smith|as)\s+(\w[\w-]*)(.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ApproveCmd =
        new(@"^/approve(?:\s+(.+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RejectCmd =
        new(@"^/reject(?:\s+(.+))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static CommentIntentType Parse(string body, out string? pipeline,
        out string? arguments, out string? dialogueComment)
    {
        pipeline = null;
        arguments = null;
        dialogueComment = null;

        var trimmed = body.Trim();

        // Scenario B: /approve or /reject
        var approveMatch = ApproveCmd.Match(trimmed);
        if (approveMatch.Success)
        {
            dialogueComment = approveMatch.Groups[1].Value.NullIfEmpty();
            return CommentIntentType.DialogueApprove;
        }

        var rejectMatch = RejectCmd.Match(trimmed);
        if (rejectMatch.Success)
        {
            dialogueComment = rejectMatch.Groups[1].Value.NullIfEmpty();
            return CommentIntentType.DialogueReject;
        }

        // Scenario A: /agent-smith <cmd>
        var asMatch = AgentSmithCmd.Match(trimmed);
        if (asMatch.Success)
        {
            var cmd = asMatch.Groups[1].Value.ToLowerInvariant();
            arguments = asMatch.Groups[2].Value.Trim().NullIfEmpty();

            pipeline = cmd switch
            {
                "fix"            => "fix-bug",
                "security-scan"  => "security-scan",
                "security"       => "security-scan",
                "review"         => "pr-review",
                "help"           => null,
                _                => cmd,   // direct as pipeline name
            };

            return cmd == "help"
                ? CommentIntentType.Help
                : CommentIntentType.NewJob;
        }

        return CommentIntentType.Unknown;
    }
}
```

---

## GitHubPrCommentWebhookHandler

Responds to two GitHub event types:

| Event | Action | Meaning |
|---|---|---|
| `issue_comment` | `created` | Comment on PR (GitHub treats PRs as issues) |
| `pull_request_review_comment` | `created` | Inline code comment |

```csharp
public sealed class GitHubPrCommentWebhookHandler(
    CommentIntentRouter router,
    IWebhookVerifier verifier,
    ILogger<GitHubPrCommentWebhookHandler> logger)
    : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "github" &&
        eventType is "issue_comment" or "pull_request_review_comment";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers, CancellationToken ct)
    {
        // 1. Verify signature
        if (!verifier.Verify("github", payload, headers))
            return WebhookResult.Unauthorized;

        // 2. Parse payload
        var doc = JsonDocument.Parse(payload).RootElement;

        // Only created comments, no edits
        if (doc.GetString("action") != "created")
            return WebhookResult.Ignored("Not a created event");

        // Only PR comments (issue_comment has pull_request URL)
        var isPr = doc.TryGetProperty("issue", out var issue) &&
                   issue.TryGetProperty("pull_request", out _);
        if (!isPr && !doc.TryGetProperty("pull_request", out _))
            return WebhookResult.Ignored("Not a PR comment");

        var body = doc.GetProperty("comment").GetString("body") ?? "";
        var author = doc.GetProperty("comment")
                        .GetProperty("user").GetString("login") ?? "";
        var repoFullName = doc.GetProperty("repository").GetString("full_name") ?? "";
        var prNumber = (doc.TryGetProperty("issue", out var iss)
            ? iss.GetInt32("number")
            : doc.GetProperty("pull_request").GetInt32("number")).ToString();
        var commentId = doc.GetProperty("comment").GetInt64("id").ToString();

        var intent = new CommentIntent(
            CommentIntentType.Unknown, // set by parser
            "github", repoFullName, prNumber, commentId, author,
            null, null, null, body);

        return await router.RouteAsync(intent, ct);
    }
}
```

---

## CommentIntentRouter

The heart — decides whether Scenario A or B and routes accordingly.

```csharp
// AgentSmith.Application/Webhooks/CommentIntentRouter.cs

public sealed class CommentIntentRouter(
    IConversationStateManager stateManager,
    IDialogueTransport dialogueTransport,   // Phase 58
    IJobEnqueuer jobEnqueuer,
    IPrCommentReplyService replyService,
    ILogger<CommentIntentRouter> logger)
{
    public async Task<WebhookResult> RouteAsync(CommentIntent raw, CancellationToken ct)
    {
        // Parse intent type
        var type = CommentIntentParser.Parse(
            raw.CommentBody,
            out var pipeline,
            out var arguments,
            out var dialogueComment);

        var intent = raw with
        {
            Type = type,
            Pipeline = pipeline,
            RawArguments = arguments,
            DialogueComment = dialogueComment,
        };

        return intent.Type switch
        {
            CommentIntentType.DialogueApprove => await HandleDialogueAnswerAsync(intent, "yes", ct),
            CommentIntentType.DialogueReject  => await HandleDialogueAnswerAsync(intent, "no", ct),
            CommentIntentType.NewJob          => await HandleNewJobAsync(intent, ct),
            CommentIntentType.Help            => await HandleHelpAsync(intent, ct),
            CommentIntentType.Unknown         => WebhookResult.Ignored("No known command"),
            _ => WebhookResult.Ignored("Unhandled intent type"),
        };
    }

    // ─── Scenario B: Dialogue answer ─────────────────────────────────────

    private async Task<WebhookResult> HandleDialogueAnswerAsync(
        CommentIntent intent, string answer, CancellationToken ct)
    {
        // Is there a running job for this PR?
        var state = await stateManager.GetByPrAsync(
            intent.Platform, intent.RepoFullName, intent.PrIdentifier, ct);

        if (state is null)
        {
            logger.LogInformation(
                "No active job for PR {Repo}#{Pr} — dialogue answer ignored",
                intent.RepoFullName, intent.PrIdentifier);

            await replyService.ReplyAsync(intent,
                "ℹ️ No active Agent Smith job for this PR.", ct);
            return WebhookResult.Ignored("No active job");
        }

        if (state.PendingQuestionId is null)
        {
            await replyService.ReplyAsync(intent,
                "ℹ️ Agent Smith is not currently waiting for an answer.", ct);
            return WebhookResult.Ignored("No pending question");
        }

        // Send answer via Redis to the running job
        var dialogueAnswer = new DialogAnswer(
            state.PendingQuestionId,
            answer,
            intent.DialogueComment,
            DateTimeOffset.UtcNow,
            intent.AuthorLogin);

        await dialogueTransport.PublishAnswerAsync(state.JobId, dialogueAnswer, ct);
        await stateManager.ClearPendingQuestionAsync(
            intent.Platform, intent.RepoFullName, intent.PrIdentifier, ct);

        // Acknowledge in PR
        var ack = answer == "yes"
            ? $"✅ **{intent.AuthorLogin}** approved."
            : $"❌ **{intent.AuthorLogin}** rejected.";
        if (intent.DialogueComment is not null)
            ack += $"\n> {intent.DialogueComment}";
        await replyService.ReplyAsync(intent, ack, ct);

        return WebhookResult.Handled("Dialogue answer forwarded");
    }

    // ─── Scenario A: New job ─────────────────────────────────────────────

    private async Task<WebhookResult> HandleNewJobAsync(
        CommentIntent intent, CancellationToken ct)
    {
        // Is there already a running job for this PR?
        var existing = await stateManager.GetByPrAsync(
            intent.Platform, intent.RepoFullName, intent.PrIdentifier, ct);

        if (existing is not null)
        {
            await replyService.ReplyAsync(intent,
                $"⏳ A job is already running for this PR. " +
                $"Please wait until it finishes.", ct);
            return WebhookResult.Ignored("Job already running");
        }

        // Build PR context as pipeline input
        // PrJobRequest → FixTicketIntent conversion
        var jobRequest = new PrJobRequest(
            Pipeline:     intent.Pipeline ?? "fix-bug",
            Platform:     intent.Platform,
            RepoFullName: intent.RepoFullName,
            PrIdentifier: intent.PrIdentifier,
            Arguments:    intent.RawArguments,
            RequestedBy:  intent.AuthorLogin,
            ChannelId:    $"pr:{intent.RepoFullName}#{intent.PrIdentifier}");

        var jobId = await jobEnqueuer.EnqueueAsync(jobRequest, ct);

        await replyService.ReplyAsync(intent,
            $"🚀 Agent Smith started (Job `{jobId}`).\n" +
            $"Pipeline: `{jobRequest.Pipeline}`\n" +
            $"I'll report back here when done or when I have questions.", ct);

        return WebhookResult.Handled($"Job {jobId} enqueued");
    }

    private async Task<WebhookResult> HandleHelpAsync(
        CommentIntent intent, CancellationToken ct)
    {
        await replyService.ReplyAsync(intent, HelpText, ct);
        return WebhookResult.Handled("Help sent");
    }

    private const string HelpText = """
        **Agent Smith — available commands:**

        `/agent-smith fix` — Start fix-bug pipeline for this PR
        `/agent-smith fix #123 in my-api` — Work on a specific ticket
        `/agent-smith security-scan` — Start security scan
        `/agent-smith review` — Start PR review pipeline

        **While a job is running:**
        `/approve` — Confirm (with optional comment)
        `/reject [reason]` — Reject

        `/agent-smith help` — Show this help
        """;
}
```

---

## PrJobRequest & IJobEnqueuer

```csharp
// AgentSmith.Contracts/Webhooks/PrJobRequest.cs

public sealed record PrJobRequest(
    string Pipeline,
    string Platform,
    string RepoFullName,
    string PrIdentifier,
    string? Arguments,
    string RequestedBy,
    string ChannelId);    // "pr:owner/repo#42" as virtual channel
```

`PrJobRequest` is converted to `FixTicketIntent` in the dispatcher:

```csharp
// Dispatcher: PrJobRequest → FixTicketIntent
var intent = new FixTicketIntent(
    TicketId: ticketId ?? $"pr-{prIdentifier}",
    ProjectName: projectName,
    Extra: new PrJobContext(prIdentifier, repoFullName, platform));
```

The agent container receives extended context for PR jobs:

```
--headless
--job-id    {jobId}
--redis-url redis:6379
--platform  github
--channel-id pr:owner/repo#42
--pr-context owner/repo#42  ← NEW: signals PR as context
fix-bug                     ← pipeline name
```

`AgenticExecuteHandler` reads `--pr-context` and loads PR description
+ open comments as additional context into the system prompt.

---

## IPrCommentReplyService

```csharp
// AgentSmith.Contracts/Services/IPrCommentReplyService.cs

public interface IPrCommentReplyService
{
    Task ReplyAsync(CommentIntent originalComment, string text, CancellationToken ct);
}
```

MVP implementation (GitHub only):

| Class | API |
|---|---|
| `GitHubPrCommentReplyService` | `POST /repos/{owner}/{repo}/issues/{pr}/comments` |

The interface is platform-agnostic — GitLab and AzDO implementations
come in p59b and p59c respectively.

---

## Signature Verification

```csharp
// AgentSmith.Infrastructure/Webhooks/WebhookVerifier.cs

public interface IWebhookVerifier
{
    bool Verify(string platform, string payload, IDictionary<string, string> headers);
}

public sealed class WebhookVerifier(WebhookSecrets secrets) : IWebhookVerifier
{
    public bool Verify(string platform, string payload, IDictionary<string, string> headers)
        => platform switch
        {
            "github" => VerifyGitHub(payload, headers),
            _        => false,   // GitLab + AzDO in p59b/p59c
        };

    // GitHub: X-Hub-Signature-256 = "sha256=" + HMAC-SHA256(secret, body)
    private bool VerifyGitHub(string payload, IDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("x-hub-signature-256", out var sig)) return false;
        if (string.IsNullOrEmpty(secrets.GitHubSecret)) return true; // dev mode
        var expected = "sha256=" + ComputeHmacSha256(secrets.GitHubSecret, payload);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sig),
            Encoding.UTF8.GetBytes(expected));
    }

    private static string ComputeHmacSha256(string secret, string payload)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(HMACSHA256.HashData(key, data)).ToLowerInvariant();
    }
}
```

---

## ConversationStateManager — PR Extension

The existing `ConversationStateManager` (p18) uses `platform + channelId` as key.
For PRs, `channelId = "pr:{repoFullName}#{prIdentifier}"` is used —
same schema, zero changes to the existing state manager.

New method for PR-specific lookups:

```csharp
public Task<ConversationState?> GetByPrAsync(
    string platform, string repoFullName, string prIdentifier,
    CancellationToken ct)
    => GetAsync(platform, $"pr:{repoFullName}#{prIdentifier}", ct);
```

---

## Config

```yaml
# agentsmith.yml

webhooks:
  github_secret: ${GITHUB_WEBHOOK_SECRET}

projects:
  my-api:
    pr_commands:
      enabled: true              # PR comment commands for this project
      allowed_users: []          # empty = all repo members allowed
      allowed_pipelines:         # which pipelines can be started via PR
        - fix-bug
        - security-scan
        - pr-review
      require_member: true       # only repo members can issue commands
```

---

## Steps

### Step 1: Contracts
- `CommentIntent`, `CommentIntentType`, `PrJobRequest`
- `IWebhookVerifier`, `IPrCommentReplyService`
- `WebhookSecrets` (config record)

### Step 2: CommentIntentParser
- Regex parser, unit tests with all command variants

### Step 3: WebhookVerifier
- GitHub HMAC-SHA256
- Dev mode (no secret = accept all)

### Step 4: GitHubPrCommentWebhookHandler
- `issue_comment` + `pull_request_review_comment` event handling

### Step 5: GitHubPrCommentReplyService
- `POST /issues/{pr}/comments`

### Step 6: CommentIntentRouter
- Scenario A + B routing
- `ConversationStateManager.GetByPrAsync`

### Step 7: WebhookListener refactoring (p43e)
- Dispatch pattern, `IWebhookHandler` DI registration
- Extract existing GitHub Issues handler

### Step 8: PrJobRequest in IJobEnqueuer
- Agent container: `--pr-context` parameter
- `AgenticExecuteHandler`: PR description + comments as context

### Step 9: Dialogue integration (Phase 58 Step 5)
- `PrCommentAdapter` as `IPlatformAdapter` implementation
- Agent posts questions as PR comment via `IPrCommentReplyService`

### Step 10: Config + DI + Tests

---

## Definition of Done

- [ ] `CommentIntent` / `CommentIntentType` / `PrJobRequest` in Contracts
- [ ] `CommentIntentParser` with all command variants (unit tests)
- [ ] `WebhookVerifier`: GitHub HMAC-SHA256
- [ ] GitHub: PR comment starts new job (Scenario A)
- [ ] GitHub: `/approve` and `/reject` reach running job (Scenario B)
- [ ] `GitHubPrCommentReplyService` implemented
- [ ] Agent posts questions as PR comment (Phase 58 integration)
- [ ] Existing GitHub Issues webhook logic: zero regression
- [ ] `require_member` guard: only allowed users can issue commands
- [ ] `allowed_pipelines` guard: only configured pipelines can be started
- [ ] Duplicate job protection: second `/agent-smith` call while job running is rejected
- [ ] Dialogue trail in PR: question-answer visible as PR comments
- [ ] Unit tests: parser, verifier, router (all scenarios)
- [ ] Integration test: webhook → Redis → job start (GitHub mocked)

---

## Dependencies

```
p43e (Webhook Dispatch Pattern)  ← must be refactored first
p58  (Dialogue, Steps 1+2)      ← Redis transport for Scenario B

p59 Steps 1–5  →  Step 6 (Router)
                       →  Step 7 (WebhookListener)
                            →  Step 8 (PrJobRequest)
                                 →  Step 9 (Dialogue integration)
                                      →  Step 10 (Tests)

Steps 3, 4, 5 are parallelizable after Steps 1+2.
```

## Out of Scope (deferred)

| Feature | Deferred to |
|---|---|
| GitLab MR comment webhook handler | p59b |
| Azure DevOps PR comment webhook handler | p59c |
| GitLab/AzDO reply services | p59b / p59c |
| GitLab/AzDO signature verification | p59b / p59c |
