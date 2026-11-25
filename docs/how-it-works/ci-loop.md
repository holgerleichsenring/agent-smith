# CI loop and merge policy

<p class="cb-lead">A green run opens pull requests. When a person looks at them is one project setting, <code>review</code>. With <code>review: none</code> codeably merges once your CI is green and sends a red check back to be fixed; the <code>merge</code> block only says how the CI is read.</p>

<figure class="cb-shot" markdown>
![The merge tab of a project in the Config studio: review, requires start and the CI settings](../assets/screenshots/config-merge.png)
<figcaption>Config studio > Projects > Edit > <strong>merge</strong>. Everything on this page is set here, or in the project's <code>review:</code> key and <code>merge:</code> block.</figcaption>
</figure>

## When a person looks

| `review` | A series | A single ticket |
|---|---|---|
| `end` (default) | Works on its own series branch. The series master merges each ticket into it once its build and tests are green; at the end one pull request per repository goes into the default branch for you to review. | The pull requests stay open for a person. |
| `each` | No series branch. Each ticket's pull request goes into the default branch, and the series master merges it after a person approved it. | The pull requests stay open for a person. |
| `none` | No series branch. The series master merges each ticket into the default branch on green. | Each pull request reads *waiting for CI*. Once CI is green on all of them, a `merge-green` run merges them. |

A red, partial or record-only run never merges, whatever the setting. Neither does a pull request with red CI.

A series keeps the review it opened with. A series opened before the setting existed keeps its branch, or its lack of one: with a series branch it runs as `end`, without one as `each`.

## Branch protection

The host's branch protection is a limit of your environment, not a codeably setting. A merge on green into the default branch arms the host's own mechanism (auto-complete on Azure DevOps, auto-merge on GitHub, merge when the pipeline succeeds on GitLab), and the host's rules decide when it completes. Azure DevOps drops auto-complete at a merge conflict, and GitLab drops auto-merge when new commits arrive. Once the pull request merges cleanly on its new head, codeably arms it again, once per head, whoever pushed; the host's merge checks (approvals, the pipeline) still decide. If you take it off a head that hasn't changed, it stays off. GitHub keeps auto-merge through codeably's pushes, so nothing needs arming there.

Two merges go past protection: a ticket's merge into its series branch, which carries no host policy, and a merge the series master asks for with `how: now`. Those need the token's permission to bypass: on Azure DevOps *Bypass policies when completing pull requests*; on GitHub repository admin with bypassing allowed, or the token's actor on the ruleset's bypass list. GitLab has no API bypass for required approvals or pipelines, so it merges when its merge checks allow.

Nothing approves a pull request: a review the host requires is a person's, and the armed merge waits for it. codeably acts under the project's machine identity (`machine:`), so the host records every push, comment and merge under the machine. A refusal leaves the pull request open and the run's result names the reason and the missing permission.

## Reading CI

Every open pull request's CI is read with its record and shown on the ticket page: `none`, `pending`, `green` or `red`. A pending CI is read again every `ci_poll_seconds` for up to `ci_pending_seconds`, whatever the project's review, because who acts next on a pull request depends on its CI. A CI still running after `holds.escalate_after_minutes` is reported to the admin. One rule for every host: where checks are marked required, only those count; any failed check is red; otherwise anything running is pending; otherwise green.

A required build that hasn't started on the head and won't start by itself reads `not_started`. The ticket says what to do, and after `ci_settle_seconds` codeably queues it again once per spell, where the host allows it:

- **Azure DevOps**: an expired build policy, or one queued with no build on the head. Its evaluation is queued again.
- **GitHub**: a check run GitHub marked stale, which gets its Actions workflow run re-run. A required check (ruleset or branch protection) that never reported on a head that has stood for `ci_settle_seconds`, with nothing else running, has no run to re-run, so the ticket asks you to start it.
- **GitLab**: a head pipeline waiting for a manual job, which a person has to run. A head that has stood for `ci_settle_seconds` without a pipeline, when an earlier head had one or GitLab says a pipeline must pass, gets a merge-request pipeline, or a pipeline on the source branch if that's refused, as long as the default branch has a CI file.

On GitHub and GitLab, runs of a pull request from a fork are never started. A head that reports nothing into a series branch is left to the series start below.

For a red head, codeably stores which checks failed, with a bounded excerpt of the evidence (the host's summary and annotations, then the end of the log) and a link. GitHub check runs and statuses, Azure DevOps build policies and statuses, and GitLab head pipelines are all read. The token needs read access to checks and logs on GitHub, Build (read) on Azure DevOps and `read_api` on GitLab.

## Series branches

A series under `review: end` works on its own branch per repository, `agent-smith/series/<id>`, cut from the default branch by its first ticket there. Its tickets' pull requests target that branch, and the series master merges them there, past protection: the branch carries no host policy. Hosts often run their required builds only for pull requests into protected branches, so the series master can start a series pull request's CI when its head reports none, through a `merge-green` run, once per head:

- **Azure DevOps**: the build definitions whose build-validation policy covers the default branch (this repository or all of the project, exactly or by prefix) are queued on the pull request's source branch at its head; their builds there are read as its checks. Needs Policy (read) and Build (read & execute).
- **GitHub**: the workflows whose `pull_request` trigger runs for the default branch are read from their files. One whose filter already takes the series branch runs on its own; otherwise each declaring `workflow_dispatch` is dispatched on the head branch. None declaring it is said, with the fix: add `agent-smith/series/**` to its `branches`, or `workflow_dispatch`. Needs Actions: write.
- **GitLab**: a merge-request pipeline is created, or a pipeline on the source branch when that is refused. Needs `api`.

What the start said shows on the series page beside the pull request. A repository with no pull-request CI for its default branch either merges as one with no CI does. When all of a series' tickets landed, one pull request per repository takes the series branch into the default branch. Its description has a section per ticket: what was built and its pull request, what the series master did on it and why, and what it could not verify (a CI on the ticket's last head that wasn't green or was never read). A person reviews and merges it.

## The fix loop

With `review: none`, a red CI on the head the green attempt pushed starts a **fix run**. Its task is the failed checks' evidence: names, excerpts, links. It fixes, verifies locally and pushes to the same pull request, never force-pushing, and the CI watch starts again. On the ticket page the run's cause reads `ci`.

The loop stops in two cases:

- `ci_fix_attempts` fix runs in a row (default 3) didn't turn CI green, or
- a fix run pushed nothing.

Then the next run hands the ticket back in its clarification status, with the evidence on the ticket. Your comment or approval starts the next attempt, and the count starts again.

## Keeping other pull requests current

A pull request its host says conflicts with its base is brought up to date in a `refresh-base` run in every project, with no setting: it can't merge as it is, so the push is needed anyway. Each conflict on a head gets one refresh; a refused one is tried again once the branch is pushed to or a landing moves the base. A pull request without a conflict is left as it is. The refresh is pushed without force. Collisions in the `state.done` index of `.agentsmith/contexts/*/context.yaml` and in the memory index `.agentsmith/memory/MEMORY.md` are united. A conflict beyond those goes to a master that reads both sides' specs and resolves it, usually by keeping both. Before anything is pushed, the repository's declared `verify` stages build and test the merged branch, even after a clean merge, because git can merge two changes into a tree that no longer builds. A red stage goes back to the master while its repairs make progress. When the merge can't be resolved or stays red, the branch is left as it was, the reason is said on the pull request, and the ticket waits for you with that reason. Closed, running, queued and waiting tickets are skipped.

## Branches after the merge

A merge codeably performs deletes its `agent-smith/` branch on Azure Repos, GitHub and GitLab. For merges a person makes, a `branch-cleanup` run per project deletes every `agent-smith/` branch whose pull request merged or closed and that hasn't moved since. Open branches, branches another pull request targets and branches without a pull request are kept. The interval is under **Branch cleanup** in [Settings](../configure-it/settings.md).

## After the merge: Deployments

The Deployments page, right below Pull requests in the sidebar, shows what happened after the merge. It covers the last 7, 30 or 90 days and lists two things:

- **Merged**: every merged pull request of a ticket's run, with the branch it went into and the commit the merge became. GitHub names that commit as the merge commit. Azure DevOps names it as the last merge commit of a completed pull request. GitLab gives the merge commit, the squash commit for a squash merge, or the head for a fast-forward merge. codeably reads it whenever it reads a merged pull request's state, so it makes no extra call to the host.
- **Deploys by agent smith**: every deploy run codeably made, as its run page shows it, with where it stands.

A pull request merged before this was recorded shows *merge commit not read*. Nothing goes back to fill those in. A ticket the tracker never closed still gets its merged pull requests read again, so some of them fill in later.

You see the projects you may read runs in. Projects hidden from you stay hidden here too.

## The keys

```yaml
projects:
  shop:
    review: none                      # end (default) | each | none
    merge:
      ci_poll_seconds: 60             # how often open pull requests' CI is read
      ci_settle_seconds: 180          # how long "no CI" must last before it counts as none
      ci_pending_seconds: 3600        # after this a pending head leaves the fast poll
      ci_fix_attempts: 3              # fix runs in a row before the ticket comes back
      ci_excerpt_chars: 4000          # evidence kept per failed check
      ci_failed_checks_read: 5        # failed checks whose logs are read
```

Every key with its default is in the [agentsmith.yml reference](../reference/configuration/agentsmith-yml.md). A configuration that still names one of the four keys `review` replaced is told so at startup, by key; `database migrate` moved a stored configuration onto `review` once.

## Next

- [Stacked start](stacked-start.md): dependents don't wait for the merge.
- [Series run](series-run.md): many tickets, one conversation.
