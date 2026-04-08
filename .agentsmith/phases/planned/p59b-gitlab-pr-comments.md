# Phase 59b: GitLab MR Comment Webhook

## Goal

Extend p59 PR comment support to GitLab Merge Request comments.
Same `CommentIntent` model, same `CommentIntentRouter`, different webhook handler.

## Scope

- `GitLabMrCommentWebhookHandler` — handles `Note Hook` events
  where `object_attributes.noteable_type == "MergeRequest"`
- `GitLabMrCommentReplyService` — `POST /projects/{id}/merge_requests/{iid}/notes`
- `WebhookVerifier` — GitLab token verification (`X-Gitlab-Token` header, plain comparison)
- Config: `webhooks.gitlab_token: ${GITLAB_WEBHOOK_TOKEN}`

### GitLab JSON Paths

- `object_attributes.note` → comment body
- `user.username` → author login
- `project.path_with_namespace` → repo full name
- `merge_request.iid` → MR identifier

## Prerequisites

- p59 (PR Comment Webhook, GitHub) — fully implemented
- p43e (Webhook Dispatch Pattern) — implemented

## Definition of Done

- [ ] `GitLabMrCommentWebhookHandler` handles Note Hook events
- [ ] `GitLabMrCommentReplyService` posts replies to MR
- [ ] GitLab token verification in `WebhookVerifier`
- [ ] Scenario A (new job) works via GitLab MR comment
- [ ] Scenario B (dialogue answer) works via GitLab MR comment
- [ ] Unit tests for GitLab-specific payload parsing
- [ ] All existing tests green
