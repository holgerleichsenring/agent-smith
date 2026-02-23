# Phase 25: PR Review Iteration

## Goal

Agent Smith reads PR review comments, makes changes, pushes new commits.
The PR becomes a conversation, not a one-shot.

## Flow

```
Ticket → Plan → Execute → Test → Create PR → Monitor
                                                 ↓
Reviewer comments → Agent reads → Plans changes → Executes →
Tests → Pushes commit → Replies to comments → Back to Monitor
(until approved or max iterations)
```

## Requirements

### Step 1: PR Review Listener

New method on `ISourceProvider`:

```csharp
Task<IReadOnlyList<ReviewComment>> GetPullRequestReviewsAsync(
    string pullRequestId, CancellationToken ct);
```

`ReviewComment` record: Id, Author, Body, FilePath?, LineNumber?, CreatedAt.

Implement for GitHub (Octokit), GitLab (MR notes API), Azure DevOps (PR threads).

### Step 2: Review Feedback Pipeline

Mini-pipeline: `FetchReviews → AnalyzeReviews → PlanRevisions → Execute → Test → PushCommit → ReplyToReviews`

- `AnalyzeReviews` classifies: actionable / question / approval / nit
- `PlanRevisions` scoped to review feedback only
- `PushCommit` new commit on existing branch (not new PR)
- `ReplyToReviews` posts reply to each addressed comment

### Step 3: Trigger Mechanisms

1. **Webhook**: `pull_request_review` event → review pipeline
2. **Slack**: "fix PR comments on #58" → review pipeline
3. **Polling** (fallback): configurable interval

### Step 4: Guardrails

- Max review iterations: configurable, default 3
- Only act on comments from authorized reviewers (configurable)
- Skip approvals and emoji reactions
- If comment unclear: ask for clarification via reply
- Never force-push — always add commits

## Architecture

- `ReviewComment` record in Domain
- `IReviewAnalyzer` in Contracts
- `ReviewPipelineExecutor` in Application (reuses existing Execute/Test steps)
- Source provider extensions: `GetPullRequestReviewsAsync`, `ReplyToReviewAsync`
- New webhook event type in WebhookListener
- Config: `review.max_iterations`, `review.authorized_reviewers`, `review.polling_interval`

## Definition of Done

- [ ] Read PR reviews from GitHub, GitLab, Azure DevOps
- [ ] Classify review comments (actionable / question / approval / nit)
- [ ] Execute changes based on review feedback
- [ ] Push new commits to existing PR branch
- [ ] Reply to review comments with explanation
- [ ] Webhook trigger for PR review events
- [ ] Slack trigger ("fix PR comments on #58")
- [ ] Max iteration guardrail
- [ ] Unit tests for review classification
- [ ] Integration test: full review cycle on a GitHub PR
