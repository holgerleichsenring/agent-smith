# Phase 59c: Azure DevOps PR Comment Webhook

## Goal

Extend p59 PR comment support to Azure DevOps Pull Request comments.
Same `CommentIntent` model, same `CommentIntentRouter`, different webhook handler.

## Scope

- `AzureDevOpsPrCommentWebhookHandler` — handles
  `ms.vss-code.git-pullrequest-comment-event` events
- `AzureDevOpsPrCommentReplyService` —
  `POST /_apis/git/repositories/{repo}/pullRequests/{id}/threads`
- `WebhookVerifier` — Azure DevOps Basic Auth verification
- Config: `webhooks.azdo_secret: ${AZDO_WEBHOOK_SECRET}`

### Azure DevOps JSON Paths

- `resource.comment.content` → comment body
- `resource.comment.author.uniqueName` → author login
- `resource.pullRequest.repository.project.name` + `repository.name` → repo full name
- `resource.pullRequest.pullRequestId` → PR identifier

## Prerequisites

- p59 (PR Comment Webhook, GitHub) — fully implemented
- p43e (Webhook Dispatch Pattern) — implemented

## Definition of Done

- [x] `AzureDevOpsPrCommentWebhookHandler` handles PR comment events
- [x] `AzureDevOpsPrCommentReplyService` posts replies as PR thread
- [x] AzDO Basic Auth verification in `WebhookSignatureValidator`
- [x] Scenario A (new job) works via AzDO PR comment
- [x] Scenario B (dialogue answer) works via AzDO PR comment
- [x] Unit tests for AzDO-specific payload parsing (7 tests)
- [x] All existing tests green (852 tests)
