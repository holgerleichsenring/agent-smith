# PR Comment Integration

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited in the [Config studio](../../configure-it/config-studio.md); the CLI reads them from `agentsmith.yml`. `agent-smith config import` moves one into the other. See [Where configuration lives](../../configure-it/index.md).

Agent Smith can be started and answered from pull request comments, and a label on a pull request can ask for a security scan. All of it arrives through the same webhook receiver.

## Start a new job

Write a comment on a PR that starts with `/agent-smith` (or `/agentsmith`, or the short `/as`):

```
/agent-smith review                      # PR review of this pull request
/agent-smith security scan               # security scan
/agent-smith fix #123 in my-api          # code pipeline for a specific ticket
/as review
```

Everything after the prefix, every following line included, is read as free text, in any language, and resolved to a pipeline and an optional ticket reference. A comment can start three pipelines: `code`, `security-scan` and `pr-review`. A comment that resolves to anything else is ignored.

`/agent-smith help` is recognized and logged, and posts nothing back. A comment that does not start with a prefix is ordinary conversation and is ignored; so is `/as-is` or `/assign`. On a pull request agent-smith opened, a command starts a rework instead — see below. Anywhere else a bare `/agent-smith` asks for nothing and is ignored.

## Ask for a security scan with a label

Putting the label `security-review` on a pull request starts a **security-scan** of that pull request: its head branch is checked out and scanned in full. The word is yours to choose: set `pr_trigger_label` on the owning project's `github_trigger` or `gitlab_trigger`, and that label triggers too.

```yaml
projects:
  todolist:
    github_trigger:
      pr_trigger_label: needs-security-review
```

Setting it adds a word rather than replacing one: `security-review` keeps triggering, so pull requests already carrying it still work. The label is matched case-insensitively. On GitHub this is the `pull_request` event with action `labeled`; on GitLab, any update to a merge request that carries the label.

Opening or updating a pull request starts nothing. A [PR review](../pipelines/pr-review.md) starts on a command such as `/agent-smith review`.

## Who may issue commands

A command is acted on only when its author can write to the repository. The check runs after the comment is recognised as a command and before any model reads it, so an ordinary comment costs no lookup and a command from anyone else costs no tokens and starts nothing:

| Platform | Write access means | How it is checked |
|----------|--------------------|-------------------|
| GitHub | the `write`, `maintain` or `admin` role on the repository | collaborator permission API with the repo's `auth` secret |
| GitLab | Developer role or higher on the project, directly or through a group | members API with the repo's `auth` secret (`read_api`) |
| Azure DevOps | effective **Contribute** permission on the repository, however it is granted | security API with the repo's `auth` secret (Identity: Read, Security: Manage) |

The payload's `author_association` is not asked: `MEMBER` holds for any organization member and `COLLABORATOR` for a read-only one. The repository must be declared in a project's `repos:`, because that is where the server learns which instance or organization to ask. A lookup that fails, a missing token or an undeclared repository counts as no write access. A verdict is remembered for five minutes per author and repository.

The set of pipelines a comment may start is fixed in code. It is deliberately narrower than what a configured label may route to, because a comment is a lower-trust surface than your configuration.

## Webhook setup

The receiver listens at `POST /webhook` and detects the platform from the delivery. Configure a secret on each platform and set the matching environment variable on the server: `GITHUB_WEBHOOK_SECRET`, `GITLAB_WEBHOOK_TOKEN` or `AZDO_WEBHOOK_SECRET`. A platform with no secret is not verified at all. See [Webhook Configuration](../configuration/webhooks.md).

On GitHub, subscribe to:

| Event | Action | Used for |
|-------|--------|----------|
| `issue_comment` | `created` | Comment on a PR (GitHub treats PRs as issues) |
| `pull_request_review_comment` | `created` | Inline code comment on a PR |
| `pull_request` | `labeled` | Security scan on the review label |
| `pull_request_review` | `submitted` | **Pull request reviews**: Request changes on an agent-smith PR starts a rework |
| `pull_request_review` | `submitted`, `edited`, `dismissed` | Any other review (approval, comment review, dismissal) keeps the PR's record current |
| `pull_request` | `closed`, `reopened` | A merge, close or reopen keeps the PR's record current |

## What a command starts

A command that names a ticket (`/agent-smith fix #12`) is claimed like any other run of that ticket: it is admitted, queued for capacity or refused, and the pull request is told which — including when another run is already working on the ticket, in which case nothing new starts. A command without a ticket (`/as review`) runs against this pull request directly. Either way the command is read once; the run starts with the pipeline it resolved to.

Before a command without a ticket starts `pr-review`, `security-scan` or `code`, agent-smith reads the pull request's branch through the host and checks what the run's bootstrap gate would check: every context's `context.yaml` and `principles.md`. When a file is missing, nothing starts; the command gets one answer naming the branch, each context and file, and that `init-project` fixes it. When the host cannot say (the branch is gone, the host does not answer), the run starts and its own gate decides. A pull request from a fork is not read this way. The project's detail in the dashboard shows the same check for each repository's default branch: initialised, not initialised with the missing files, or why the host could not be read.

## Request changes starts a rework

Comments on a pull request agent-smith opened (head branch `agent-smith/<ticket>`, in the same repository) start nothing on their own: they are collected, and the next attempt reads the unresolved threads and everything written since the previous attempt started. A rework starts when a reviewer with write access comments `/agent-smith` (or `/agentsmith`, `/as`) — bare, or followed by further instructions on as many lines as needed — or chooses **Request changes** (GitHub) or votes **Wait for author** (Azure DevOps). A Wait for author vote counts whoever cast it, the pull request's creator included, because agent-smith only ever votes Approve. So: write your review comments, then one `/agent-smith`, and the rework works through all of them. The command is not handed to a model; on such a pull request it always means a rework. On the ticket's side, a comment with the project's `comment_keyword` or moving the finished ticket back into a trigger status starts the same rework ([Rework a finished ticket](../../trigger-it/webhooks.md#rework-a-finished-ticket)).

When the ticket's last run handed the ticket back (a question, a contradiction or a not-implementable verdict), the hand-back is posted on the pull request too. Answering there and commenting `/agent-smith` ends the waiting run and starts the rework; a reply on the pull request counts as a reply to the hand-back. Our own notes on the pull request never start anything. The run then reads that act from the pull request itself, so it is served whichever trigger claims the ticket, and the pull request names the run. While a run is working on the ticket, a review is not picked up: the pull request says so, and you request changes again once that run ends — on Azure DevOps reset your vote first, since only a change of vote is sent.

When a person closed the ticket in its tracker, a Request changes or an `/agent-smith` command does not reopen it: agent-smith files one follow-up ticket carrying the review's text (or the pull request's link for a vote without text), links the two tickets, and says on the pull request where the work continues. A ticket agent-smith itself moved to `done_status`, or closed when none is set, is reworked as before.

On GitHub and GitLab the pull request's own author never counts as a reviewer: if the token agent-smith uses belongs to you, you are that author, and your own Request changes is ignored. On Azure DevOps a person whose account is the token's still starts a rework with **Wait for author**, and the ticket page lists that vote as changes requested; agent-smith's own auto-complete approval is still not listed as an approval.

On GitLab (19.3 or later), **Request changes** on an agent-smith merge request starts a rework the same way. Enable **Merge request events** on the project webhook; the token behind the repository's `auth` secret needs the `api` scope (it reads notes, users and members and posts the answer). The act and its time are GitLab's own "requested changes" system note, so a repeated Request changes after the next attempt starts another one; an approval by the same reviewer after the request withdraws it. The webhook fires only for a reviewer listed on the merge request: a reviewer GitLab could not add (for example a second reviewer on a single-reviewer merge request) sends no delivery, and their request is read only by the ticket's next run.

A comment counts as agent-smith's own only when it carries the agent-smith marker **and** was written by the account behind the repository's token, on every host; a pasted marker is a stranger's comment, never collected as ours and never deleted when a review is re-posted.

On Azure DevOps, add a **Pull request updated** subscription filtered to **Reviewer vote changed** (`ReviewerVoteNotification`) with the HTTP header `X-AgentSmith-Change: review-vote` and the same Basic credentials. A **Pull request created** or **Source branch updated** subscription is no longer needed: neither starts anything.

## The pull request's record

agent-smith keeps a record of every pull request a ticket's runs opened: its state, the reviewers' standing decisions and the comments. Besides a run's end and a look at the ticket, these deliveries ask for the record to be read again. None of them starts, refuses or announces anything; a Request changes still starts a rework exactly as above.

| Host | Delivery | Subscribe to |
|------|----------|--------------|
| GitHub | every review (approved, commented, edited, dismissed), every comment — a top-level comment, a review comment with its file and line — and a pull request closed (merged or not) or reopened | **Pull request reviews**, **Issue comments**, **Pull request review comments**, **Pull requests** |
| GitLab | a reviewer's update, every comment, and a merge request closed, merged, reopened, approved or unapproved | **Merge request events**, **Comments** |
| Azure DevOps | every vote (an approval, a reset to 0 that withdraws a decision), every comment, a pull request completed or abandoned, a merge attempted | the reviewer-vote **Pull request updated** subscription above, **Pull request commented on**, a second **Pull request updated** subscription filtered to **Status changed** (no header), and **Pull request merge attempted** |

Our own notes never refresh anything. A delivery arriving while the ticket's nudge is backing off is read at once; a reopen that coincides with another pull request's delivery is caught by the next read of the ticket's recently closed records, which runs for a day after a pull request ends. In polling mode the same changes are found by the sweeps; see [polling](../../trigger-it/polling.md#polling-vs-webhooks) for when.

## How it works

```
PR comment / PR label
    |
    v
POST /webhook (signature verified when a secret is configured)
    |
    +-- comment  --> /agent-smith or /as  --> pipeline resolved from the text --> job starts
    |            --> anything else        --> ignored
    |
    +-- label    --> security-review or pr_trigger_label --> security-scan starts
    |
    +-- opened / pushed to --> nothing starts
```

## Platform support

| Platform | Comment commands | Review label |
|----------|------------------|--------------|
| GitHub | Supported | Supported |
| GitLab (merge requests) | Supported | Supported |
| Azure DevOps | Supported | — |

See also: [Webhook Configuration](../configuration/webhooks.md) for the full configuration reference.
