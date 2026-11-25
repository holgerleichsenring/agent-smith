# CI loop and merge policy

<p class="cb-lead">A green run opens pull requests. Whether codeably also merges them, waits for your CI first, and sends a red check back to be fixed is set per project in its <code>merge</code> block.</p>

<figure class="cb-shot" markdown>
![The merge tab of a project in the Config studio: requires start, merge when, respect branch protection, refresh open pull requests and the CI settings](../assets/screenshots/config-merge.png)
<figcaption>Config studio > Projects > Edit > <strong>merge</strong>. Everything on this page is set here, or in the project's <code>merge:</code> block.</figcaption>
</figure>

## When to merge

| `merge.when` | What happens after a green run |
|---|---|
| `manual` (default) | The pull requests stay open for a person. |
| `on_gate_green` | The run merges its own pull requests as its last step, once every spec passed verification. Later work fixes forward. |
| `on_ci_green` | Each pull request reads *waiting for CI*. Once CI is green on all of them, a `merge-green` run merges them. |

A red, partial or record-only run never merges, whatever the policy. Neither does a pull request with red CI.

## Branch protection

`respect_branch_protection` decides who has the last word:

- **Off (default):** the merge goes past branch policies, required reviews and required checks, which exist for people. The token needs the permission to bypass them: on Azure DevOps *Bypass policies when completing pull requests*; on GitHub repository admin with bypassing allowed, or the token's actor on the ruleset's bypass list. GitLab has no API bypass for required approvals or pipelines, so it merges when its merge checks allow.
- **On:** codeably arms the host's own mechanism (auto-complete on Azure DevOps, auto-merge on GitHub, merge when the pipeline succeeds on GitLab) and the host's rules decide. Azure DevOps drops auto-complete at a merge conflict, and GitLab drops auto-merge when new commits arrive. Once the pull request merges cleanly on its new head, codeably arms it again, once per head, whoever pushed; the host's merge checks (approvals, the pipeline) still decide. If you take it off a head that hasn't changed, it stays off. GitHub keeps auto-merge through codeably's pushes, so nothing needs arming there.

Where the host requires an approval, `approval_auth` names the secret of a second identity that approves. GitHub never lets the author approve its own pull request, so a required review there needs one. A refusal leaves the pull request open and the run's result names the reason, the missing permission and the key to change.

## Reading CI

Every open pull request's CI is read with its record and shown on the ticket page: `none`, `pending`, `green` or `red`. A pending CI is read again every `ci_poll_seconds` for up to `ci_pending_seconds`, whatever the project's merge mode, because who acts next on a pull request depends on its CI. A CI still running after `holds.escalate_after_minutes` is reported to the admin. One rule for every host: where checks are marked required, only those count; any failed check is red; otherwise anything running is pending; otherwise green.

A required build that hasn't started on the head and won't start by itself reads `not_started`. The ticket says what to do, and after `ci_settle_seconds` codeably queues it again once per spell, where the host allows it:

- **Azure DevOps**: an expired build policy, or one queued with no build on the head. Its evaluation is queued again.
- **GitHub**: a check run GitHub marked stale, which gets its Actions workflow run re-run. A required check (ruleset or branch protection) that never reported on a head that has stood for `ci_settle_seconds`, with nothing else running, has no run to re-run, so the ticket asks you to start it.
- **GitLab**: a head pipeline waiting for a manual job, which a person has to run. A head that has stood for `ci_settle_seconds` without a pipeline, when an earlier head had one or GitLab says a pipeline must pass, gets a merge-request pipeline, or a pipeline on the source branch if that's refused, as long as the default branch has a CI file.

On GitHub and GitLab, runs of a pull request from a fork are never started. A head that reports nothing into a series branch is left to the series start below.

For a red head, codeably stores which checks failed, with a bounded excerpt of the evidence (the host's summary and annotations, then the end of the log) and a link. GitHub check runs and statuses, Azure DevOps build policies and statuses, and GitLab head pipelines are all read. The token needs read access to checks and logs on GitHub, Build (read) on Azure DevOps and `read_api` on GitLab.

## Series branches

A series of two or more tickets works on its own branch per repository, `agent-smith/series/<id>`, cut from the default branch by its first ticket there. Its tickets' pull requests target that branch and merge under `merge.series_when` (default `on_ci_green`), always past protection: the branch carries no host policy. Hosts often run their required builds only for pull requests into protected branches, so a series pull request whose head reports no CI for `ci_settle_seconds` gets its repository's pull-request CI started by a `merge-green` run, once per head, whatever `series_when` says:

- **Azure DevOps**: the build definitions whose build-validation policy covers the default branch (this repository or all of the project, exactly or by prefix) are queued on the pull request's source branch at its head; their builds there are read as its checks. Needs Policy (read) and Build (read & execute).
- **GitHub**: the workflows whose `pull_request` trigger runs for the default branch are read from their files. One whose filter already takes the series branch runs on its own; otherwise each declaring `workflow_dispatch` is dispatched on the head branch. None declaring it is said, with the fix: add `agent-smith/series/**` to its `branches`, or `workflow_dispatch`. Needs Actions: write.
- **GitLab**: a merge-request pipeline is created, or a pipeline on the source branch when that is refused. Needs `api`.

What the start said shows on the series page beside the pull request. A repository with no pull-request CI for its default branch either merges as one with no CI does. When all of a series' tickets landed, one pull request per repository takes the series branch into the default branch under `merge.when`.

## The fix loop

With `on_ci_green`, a red CI on the head the green attempt pushed starts a **fix run**. Its task is the failed checks' evidence: names, excerpts, links. It fixes, verifies locally and pushes to the same pull request, never force-pushing, and the CI watch starts again. On the ticket page the run's cause reads `ci`.

The loop stops in two cases:

- `ci_fix_attempts` fix runs in a row (default 3) didn't turn CI green, or
- a fix run pushed nothing.

Then the next run hands the ticket back in its clarification status, with the evidence on the ticket. Your comment or approval starts the next attempt, and the count starts again.

## Keeping other pull requests current

A pull request its host says conflicts with its base is brought up to date in a `refresh-base` run in every project, with no setting: it can't merge as it is, so the push is needed anyway. Each conflict on a head gets one refresh; a refused one is tried again once the branch is pushed to or a landing moves the base. With `refresh_open_pull_requests: true`, a landing also merges its base into every other open codeably pull request on the same repository and base, conflict or not, to keep the branches current. Either way the refresh is pushed without force. Collisions in the `state.done` index of `.agentsmith/contexts/*/context.yaml` and in the memory index `.agentsmith/memory/MEMORY.md` are united. A conflict beyond those goes to a master that reads both sides' specs and resolves it, usually by keeping both. Before anything is pushed, the repository's declared `verify` stages build and test the merged branch, even after a clean merge, because git can merge two changes into a tree that no longer builds. A red stage goes back to the master while its repairs make progress. When the merge can't be resolved or stays red, the branch is left as it was, the reason is said on the pull request, and the ticket waits for you with that reason. Closed, running, queued and waiting tickets are skipped.

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
    merge:
      when: on_ci_green               # manual | on_gate_green | on_ci_green
      respect_branch_protection: false
      refresh_open_pull_requests: false
      ci_poll_seconds: 60             # how often open pull requests' CI is read
      ci_settle_seconds: 180          # how long "no CI" must last before it counts as none
      series_when: on_ci_green        # a series' ticket pull requests into its series branch
      ci_pending_seconds: 3600        # after this a pending head leaves the fast poll
      ci_fix_attempts: 3              # fix runs in a row before the ticket comes back
      ci_excerpt_chars: 4000          # evidence kept per failed check
      ci_failed_checks_read: 5        # failed checks whose logs are read
      approval_auth: github_approver  # optional: secret of the approving identity
```

Every key with its default is in the [agentsmith.yml reference](../reference/configuration/agentsmith-yml.md).

## Next

- [Stacked start](stacked-start.md): dependents don't wait for the merge.
- [Series run](series-run.md): many tickets, one conversation.
