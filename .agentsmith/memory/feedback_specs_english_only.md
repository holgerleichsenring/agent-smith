---
name: feedback_specs_english_only
description: "Phase YAML specs (.agentsmith/phases/) and any project docs in this repo must be written in English. Never German, even when the user writes to me in German."
metadata:
  type: feedback
status: proposed
---
Phase YAML files under `.agentsmith/phases/` and other project documentation are written exclusively in English. This applies regardless of the language the user uses in conversation with me — they may write in German, but the artifacts that go into the repo are English.

**Why:** The user pointed this out after I had committed German content into a spec: "das ist alles auf deutsch. das ist mir entgangen. bitte die specs alle auf englisch übersetzen die es nicht sind (die sind NIE auf deutsch)." The repo is consumed by tooling, contributors, and reviewers that expect English. Mixed-language artifacts break consistency and accessibility.

**How to apply:**
- When writing or editing a phase spec (`.agentsmith/phases/**/*.yaml`), use English. Same for the spec's `goal`, `decisions`, `steps`, `done`, frontmatter — every prose field.
- Decision files (`.agentsmith/decisions/**/*.{md,yaml}` — including the planned post-v2.0 YAML shape) are English-only too. User confirmed 2026-05-24: "alles ist immer auf englisch. bitte in decision.yaml berücksichtigen."
- Same applies to project docs: `docs/**/*.md`, README, CHANGELOG, code comments, commit messages, PR descriptions.
- If the user writes a German spec or doc and asks me to commit it, translate to English first (or flag it before committing if the user clearly authored it that way and might prefer a discussion).
- Conversational replies to the user can stay in German when they write to me in German — that's a different register from committed artifacts.
- Memory files in this directory: same English-only rule, since they're effectively repo-adjacent documentation.
