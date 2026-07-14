# PR review

The `pr-review` pipeline reviews a pull request the way the other pipelines fix a ticket: it fetches the PR diff, dispatches a set of review skills over it, and posts the findings back onto the PR as line-anchored comments.

## What it does

- The input is the PR diff, not the whole repo — the review skills see what changed, with enough context to judge it.
- Four review skills ship in the catalog, each with its own lens (correctness, design, security, tests). Each emits findings anchored to a file and line in the diff.
- Findings land as review comments on the PR, on the lines they're about.
- **Re-review overwrites.** Push a new commit and the next review replaces its previous comments instead of piling a second opinion on top of a stale one.

## Triggering

Like every pipeline: a label (`pipeline_from_label` mapping to `pr-review`), or the PR-comment path described in [PR comments](../integrations/pr-comments.md).

## Tuning the blocking bar

A finding can be blocking. Whether it's *allowed* to block is gated by confidence:

```yaml
projects:
  todolist:
    # ...
    pipelines:
      - name: pr-review
        confidence_threshold: 70   # default 70, range 0-100
```

A blocking observation below the threshold is downgraded to non-blocking before it can gate anything. Lower the threshold to let more findings block; raise it to keep only the high-confidence ones. Per-pipeline entries can also override `agent` and `skills_path` if you want a different model or your own review skills for this pipeline.

## Next

- [PR comments](../integrations/pr-comments.md) — the comment-driven interaction on PRs.
- [Fix Bug / Add Feature](fix-and-feature.md) — the pipelines whose output you'd be reviewing.
