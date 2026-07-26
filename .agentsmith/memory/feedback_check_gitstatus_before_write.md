---
name: feedback_check_gitstatus_before_write
description: "Before Write-creating a file, check the session-start git-status snapshot for that path — an untracked same-name file may be pre-existing work"
metadata:
  type: feedback
status: proposed
---
Before creating a file with Write, check whether the path already appears in the session-start git-status snapshot (the `?? path` lines in the environment header). If it does, an untracked file already exists there — Write will silently clobber it and, because it is untracked, git cannot recover it.

This happened in 2026-07: I independently designed a "connection diagnostics" phase and `Write`-created `.agentsmith/phases/planned/p0284-connection-diagnostics.yaml`, overwriting a pre-existing untracked draft of the same name. It turned out to be an earlier AI draft (not the user's hand-work) AND mis-numbered (p0284 was already merged as jira-endpoints-overridable), so no real loss — but I only discovered that by reconstructing from Time-Machine snapshots and `~/.claude/projects/*.jsonl` transcripts after the fact.

**Why:** untracked files are invisible to git recovery; the session-start snapshot is the one place their prior existence is recorded.

**How to apply:** when a Write target's path matches an untracked entry in the start-of-conversation git status, Read it first (or `git stash`/copy it) and reconcile before overwriting. Prior-session Write contents are recoverable from `~/.claude/projects/<slug>/*.jsonl` transcripts; untracked file bodies at a point in time are in Time-Machine local snapshots (needs sudo to mount). Relates to [[feedback_phase_versioning]] (never mutate an existing phase spec — create a successor) and [[feedback_used_to_work_check_history]].
