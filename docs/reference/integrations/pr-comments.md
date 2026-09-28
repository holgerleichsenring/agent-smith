# PR Comment Integration

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited in the [Config studio](../../configure-it/config-studio.md); the CLI reads them from `agentsmith.yml`. `agent-smith config import` moves one into the other. See [Where configuration lives](../../configure-it/index.md).

Agent Smith can be started and answered from pull request comments, and a label on a pull request can ask for a security scan. All of it arrives through the same webhook receiver.

## Start a new job

Write a comment on a PR that starts with `/agent-smith` (or the short `/as`):

```
/agent-smith review                      # PR review of this pull request
/agent-smith security scan               # security scan
/agent-smith fix #123 in my-api          # code pipeline for a specific ticket
/as review
```

Everything after the prefix is read as free text, in any language, and resolved to a pipeline and an optional ticket reference. A comment can start three pipelines: `code`, `security-scan` and `pr-review`. A comment that resolves to anything else is ignored.

`/agent-smith help` is recognized and logged, and posts nothing back. A comment that does not start with a prefix is ordinary conversation and is ignored.

## Answer a running job

When a running job asks a question on the PR (via [Interactive Dialogue](../concepts/interactive-dialogue.md)), answer with:

```
/approve                                 # confirm (yes)
/approve Please rename the branch        # confirm with comment
/reject                                  # reject (no)
/reject The naming convention is wrong   # reject with reason
```

Commands are case-insensitive. The answer is forwarded to the running job, and the pipeline continues.

## Ask for a security scan with a label

Putting the label `security-review` on a pull request starts a **security-scan** of that repository. The word is yours to choose: set `pr_trigger_label` on the owning project's `github_trigger` or `gitlab_trigger`, and that label triggers too.

```yaml
projects:
  todolist:
    github_trigger:
      pr_trigger_label: needs-security-review
```

Setting it adds a word rather than replacing one: `security-review` keeps triggering, so pull requests already carrying it still work. The label is matched case-insensitively. On GitHub this is the `pull_request` event with action `labeled`; on GitLab, any update to a merge request that carries the label.

Opening or updating a pull request starts a [PR review](../pipelines/pr-review.md) on its own, without a label or a comment.

## Who may issue commands

On GitHub, only comments whose `author_association` is `OWNER`, `MEMBER`, `COLLABORATOR` or `CONTRIBUTOR` are acted on; anyone else is ignored. The GitLab and Azure DevOps handlers do not check the author, so restrict who can comment on those repositories if that matters to you.

The set of pipelines a comment may start is fixed in code. It is deliberately narrower than what a configured label may route to, because a comment is a lower-trust surface than your configuration.

## Webhook setup

The receiver listens at `POST /webhook` and detects the platform from the delivery. Configure a secret on each platform and set the matching environment variable on the server: `GITHUB_WEBHOOK_SECRET`, `GITLAB_WEBHOOK_TOKEN` or `AZDO_WEBHOOK_SECRET`. A platform with no secret is not verified at all. See [Webhook Configuration](../configuration/webhooks.md).

On GitHub, subscribe to:

| Event | Action | Used for |
|-------|--------|----------|
| `issue_comment` | `created` | Comment on a PR (GitHub treats PRs as issues) |
| `pull_request_review_comment` | `created` | Inline code comment on a PR |
| `pull_request` | `opened`, `synchronize`, `labeled` | PR review on open and push; security scan on the review label |

## How it works

```
PR comment / PR label / PR event
    |
    v
POST /webhook (signature verified when a secret is configured)
    |
    +-- comment  --> /agent-smith or /as  --> pipeline resolved from the text --> job starts
    |            --> /approve or /reject  --> answer forwarded to the running job
    |            --> anything else        --> ignored
    |
    +-- label    --> security-review or pr_trigger_label --> security-scan starts
    |
    +-- opened / updated --> pr-review starts (or the pipeline a mapped PR label names)
```

## Platform support

| Platform | Comment commands | Review label | Review on open/update |
|----------|------------------|--------------|-----------------------|
| GitHub | Supported | Supported | Supported |
| GitLab (merge requests) | Supported | Supported | Supported |
| Azure DevOps | Supported | — | Supported |

See also: [Webhook Configuration](../configuration/webhooks.md) for the full configuration reference.
