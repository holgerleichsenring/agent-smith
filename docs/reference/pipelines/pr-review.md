# PR review

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited in the [Config studio](../../configure-it/config-studio.md); the CLI reads them from `agentsmith.yml`. `agent-smith config import` moves one into the other. See [Where configuration lives](../../configure-it/index.md).

The `pr-review` pipeline reviews a pull request: it reads the PR diff, has the **pr-review-master** review it, and posts the findings back onto the PR as line-anchored comments.

## What it does

- The input is the PR diff, with the repository checked out at the PR's head so the master can read the code around a change.
- One master covers four dimensions: correctness, a thin security pass over the changed lines, style, and test coverage. On a large diff it splits the review across sub-agents and merges what comes back. A security concern that needs the whole repository is noted once and points you at a full `security-scan`.
- Every finding the master delivers is put to a fresh instance asked to refute it, the same step the scans run. A refuted finding is downgraded to Medium and carries the reason. See [Security Scan](security-scan.md#every-delivered-finding-faces-a-refuter).
- Findings land as review comments on the lines they're about, grouped by file and line range, most severe first, at most 25 inline. The rest, and any finding on a line the diff doesn't touch, fold into one summary comment. A review with no findings still posts a summary.
- **Re-review replaces.** Each comment carries a hidden marker. When the PR is updated, the next review deletes the previous review's comments before posting the new ones, instead of piling a second opinion on top of a stale one.

## Pipeline steps

| # | Command | What it does |
|---|---------|-------------|
| 1 | LoadCatalog | Pulls and verifies the skill catalog |
| 2 | PipelineNameInitializer | Stamps the pipeline name for master routing |
| 3 | CheckoutSource | Checks out the PR's head branch |
| 4-5 | BootstrapCheck / BootstrapGate | Aborts early if the repo was never initialized |
| 6 | LoadCodingPrinciples | Loads the repo's principles |
| 7 | LoadMemoryIndex | Loads the project's recorded memory |
| 8 | LoadContext | Loads the project's `.agentsmith/` context files |
| 9 | AnalyzeCode | Scout agent maps the code |
| 10 | AnalyzePrDiff | Reads the PR's head and base and the per-file patches |
| 11 | AgenticMaster | Runs the pr-review-master over the diff |
| 12 | MergeMasterFindings | Takes the master's findings as the delivered set |
| 13 | SubstantiateFindings | Puts every finding to a refuter |
| 14 | CompilePrReviewFindings | Groups findings into inline comments and a summary |
| 15 | WriteRunResult | Writes `result.md` |
| 16 | PostPrComments | Deletes the previous review's comments and posts the new batch |

The PR must live in a GitHub, GitLab or Azure DevOps repository; any other repo type fails at `PostPrComments`.

## Triggering

A PR being **opened or updated** starts a review, when the repository belongs to a configured project. On GitHub that is the `pull_request` event with action `opened` or `synchronize`; on GitLab a merge request opened or pushed to; on Azure DevOps `git.pullrequest.created` and `git.pullrequest.updated`. A repository no project claims never triggers.

The project's platform trigger (`github_trigger`, `gitlab_trigger` or `azuredevops_trigger`) can route a PR elsewhere with `pipeline_from_label`: a PR carrying a mapped label runs the pipeline that label names instead of `pr-review`.

```yaml
projects:
  todolist:
    github_trigger:
      pipeline_from_label:
        no-review: security-scan     # this PR gets a security scan instead of a review
```

You can also start a review from a PR comment, `/agent-smith review`; see [PR comments](../integrations/pr-comments.md).

!!! note "The review label starts a security scan"
    Putting the label `security-review` on a pull request, or the word your project sets as `pr_trigger_label` on its `github_trigger` / `gitlab_trigger`, starts a **security-scan** of that pull request's head (its branch checked out, scanned in full), not a `pr-review`. The scan starts when the label is added, and only for a repository a project configures. Setting `pr_trigger_label` adds a word; `security-review` keeps triggering either way. Pushes to a pull request that already carries the label still start a `pr-review`.

## Per-pipeline overrides

A project's `pipelines:` entry for `pr-review` can set its own `agent` and `skills_path`, if you want a different model or your own review master for this pipeline. See [Pipelines](index.md#per-project-pipeline-settings).

## Next

- [PR comments](../integrations/pr-comments.md) — the comment-driven interaction on PRs.
- [Fix Bug / Add Feature](fix-and-feature.md) — the pipeline whose output you'd be reviewing.
