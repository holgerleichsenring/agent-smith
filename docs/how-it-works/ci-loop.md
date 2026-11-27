# CI loop and merge policy

<p class="cb-lead">A green run opens pull requests. Every ticket is led by a series master: a filed series, or a single ticket as a series of one. When a person looks at its pull requests is one project setting, <code>review</code>. The <code>merge</code> block only says how the CI is read.</p>

<figure class="cb-shot" markdown>
![The merge tab of a project in the Config studio: review, requires start and the CI settings](../assets/screenshots/config-merge.png)
<figcaption>Config studio > Projects > Edit > <strong>merge</strong>. Everything on this page is set here, or in the project's <code>review:</code> key and <code>merge:</code> block.</figcaption>
</figure>

## When a person looks

| `review` | A series | A single ticket (a series of one) |
|---|---|---|
| `end` (default) | Works on its own series branch. The series master merges each ticket into it once its build and tests are green; before the final pull request per repository goes into the default branch, it asks you. | Same as `each`: with one ticket there is one flow. |
| `each` | No series branch. Each ticket's pull request goes into the default branch; the series master asks you after each ticket. | The ticket page's *Your move* box asks: **Merge** merges it under codeably's identity, or look at the pull request first. Merging on the host works too; the master follows. |
| `none` | No series branch. The series master merges each ticket into the default branch on green. | The series master merges it on green. |

Every merge question offers **Merge it** and **Look at the pull request first** as buttons, with a link to each pull request, and never takes a default: nothing merges because nobody answered. codeably never approves a pull request on the host.

A red, partial or record-only run never merges, whatever the setting. Neither does a pull request with red CI.

A series keeps the review it opened with. A series opened before the setting existed keeps its branch, or its lack of one: with a series branch it runs as `end`, without one as `each`.

## Branch protection

The host's branch protection is a limit of your environment, not a codeably setting. A merge on green into the default branch arms the host's own mechanism (auto-complete on Azure DevOps, auto-merge on GitHub, merge when the pipeline succeeds on GitLab), and the host's rules decide when it completes. Azure DevOps drops auto-complete at a merge conflict, and GitLab drops auto-merge when new commits arrive. When such a conflict clears, the series master is told and arms it again with its merge; the host's merge checks (approvals, the pipeline) still decide. GitHub keeps auto-merge through codeably's pushes, so nothing needs arming there.

Two merges go past protection: a ticket's merge into its series branch, which carries no host policy, and a merge the series master asks for with `how: now`. Those need the token's permission to bypass: on Azure DevOps *Bypass policies when completing pull requests*; on GitHub repository admin with bypassing allowed, or the token's actor on the ruleset's bypass list. GitLab has no API bypass for required approvals or pipelines, so it merges when its merge checks allow.

Nothing approves a pull request: a review the host requires is a person's, and the armed merge waits for it. codeably acts under the project's machine identity (`machine:`), so the host records every push, comment and merge under the machine. A refusal leaves the pull request open and the run's result names the reason and the missing permission.

## Reading CI

Every open pull request's CI is read with its record and shown on the ticket page: `none`, `pending`, `green` or `red`. A pending CI is read again every `ci_poll_seconds` for up to twice `ci_pending_seconds`, whatever the project's review; past `ci_pending_seconds` it wakes the series master once. A head that reports nothing is read again for up to twice `ci_settle_seconds`; once it has stood that long with nothing reported, it has no CI, and the series master is told once and merges it as it would a green one. Under `end` (into the series branch) and `none`, that merge is the master's: the ticket shows as merging, never under *Needs you*. So are a conflict (the master refreshes the branch) and a draft (the master publishes it) on any ticket a series master leads, while that master can act. A CI still running after `holds.escalate_after_minutes` is reported to the admin. One rule for every host: where checks are marked required, only those count; any failed check is red; otherwise anything running is pending; otherwise green.

A required build that hasn't started on the head and won't start by itself reads `not_started`. The ticket says what to do, and the series master can queue it again (`ci requeue`), where the host allows it:

- **Azure DevOps**: an expired build policy, or one queued with no build on the head. Its evaluation is queued again.
- **GitHub**: a check run GitHub marked stale, which gets its Actions workflow run re-run. A required check (ruleset or branch protection) that never reported on a head that has stood for `ci_settle_seconds`, with nothing else running, has no run to re-run, so the ticket asks you to start it.
- **GitLab**: a head pipeline waiting for a manual job, which a person has to run. A head that has stood for `ci_settle_seconds` without a pipeline, when an earlier head had one or GitLab says a pipeline must pass, gets a merge-request pipeline, or a pipeline on the source branch if that's refused, as long as the default branch has a CI file.

On GitHub and GitLab, runs of a pull request from a fork are never started. A head that reports nothing into a series branch is left to the series start below.

For a red head, codeably stores which checks failed, with a bounded excerpt of the evidence and a link: the failed tests with their message and stack when the host published test results (Azure DevOps test runs, GitLab test reports), else the host's summary and annotations, then the end of the failed step's log. GitHub check runs and statuses, Azure DevOps build policies and statuses, and GitLab head pipelines are all read. The token needs read access to checks and logs on GitHub, Build (read) and Test Management (read) on Azure DevOps and `read_api` on GitLab.

## Series branches

A series under `review: end` works on its own branch per repository, `agent-smith/series/<id>`, cut from the default branch by its first ticket there. Its tickets' pull requests target that branch, and the series master merges them there, past protection: the branch carries no host policy. Hosts often run their required builds only for pull requests into protected branches, so the series master can start a series pull request's CI when its head reports none, through a `merge-green` run, once per head:

- **Azure DevOps**: the build definitions whose build-validation policy covers the default branch (this repository or all of the project, exactly or by prefix) are queued on the pull request's source branch at its head; their builds there are read as its checks. Needs Policy (read) and Build (read & execute).
- **GitHub**: the workflows whose `pull_request` trigger runs for the default branch are read from their files. One whose filter already takes the series branch runs on its own; otherwise each declaring `workflow_dispatch` is dispatched on the head branch. None declaring it is said, with the fix: add `agent-smith/series/**` to its `branches`, or `workflow_dispatch`. Needs Actions: write.
- **GitLab**: a merge-request pipeline is created, or a pipeline on the source branch when that is refused. Needs `api`.

What the start said shows on the series page beside the pull request. A repository with no pull-request CI for its default branch either merges as one with no CI does. When all of a series' tickets landed, one pull request per repository takes the series branch into the default branch. Its description has a section per ticket: what was built and its pull request, what the series master did on it and why, and what it could not verify (a CI on the ticket's last head that wasn't green or was never read). A person reviews and merges it.

## A red CI

A red CI wakes the series master. It decides: retry the run when the failure wasn't the work's fault, or tell the ticket what failed with the evidence (failed checks, excerpts, links), and the ticket's run fixes it on the same pull request. The master's `ci read` answers that evidence, and every run on the ticket starts with it in its prompt. Nothing starts a fix run by itself.

## Keeping other pull requests current

A pull request its host says conflicts with its base wakes the series master, which brings it up to date in a `refresh-base` run (`refresh_branch`). A pull request without a conflict is left as it is. The refresh is pushed without force. Lines that both sides added to the done index `.agentsmith/contexts/*/done.md` or the memory index `.agentsmith/memory/MEMORY.md` merge with `merge=union`, so both sides' lines are kept. A conflict beyond those goes to a master that reads both sides' specs and resolves it, usually by keeping both. Before anything is pushed, the repository's declared `verify` stages build and test the merged branch, even after a clean merge, because git can merge two changes into a tree that no longer builds. A red stage goes back to the master while its repairs make progress. When the merge can't be resolved or stays red, the branch is left as it was, the reason is said on the pull request, and the ticket waits for you with that reason. Closed, running, queued and waiting tickets are skipped.

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
      ci_fix_attempts: 3              # repair passes a refresh-base run's merge may take
      ci_excerpt_chars: 4000          # evidence kept per failed check
      ci_failed_checks_read: 5        # failed checks whose logs are read
```

Every key with its default is in the [agentsmith.yml reference](../reference/configuration/agentsmith-yml.md). A configuration that still names one of the four keys `review` replaced is told so at startup, by key; `database migrate` moved a stored configuration onto `review` once.

## Next

- [Stacked start](stacked-start.md): dependents don't wait for the merge.
- [Series run](series-run.md): many tickets, one conversation.
