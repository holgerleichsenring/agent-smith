---
name: feedback_one_decision_yaml_per_phase
description: "Decision YAML structure is ONE file per phase, with all of that phase's decisions inside as a list. Not one file per individual decision."
metadata:
  type: feedback
status: proposed
---
The spec-first v2.0 decision file layout is **one YAML file per phase**, with all decisions for that phase contained as a list inside the file. NOT one file per individual decision.

Correct shape:
```yaml
# yaml-language-server: $schema=../decision.schema.json
phase: p0161
decisions:
  - category: Architecture
    chose: "..."
    reason: |
      ...
  - category: Tooling
    chose: "..."
    reason: |
      ...
```

Filename = `.{project}/decisions/p{NN}.yaml` (or `r{NN}.yaml` for run-attached).

**Why:** User correction 2026-05-24: "1000 yaml files? das macht doch keinen sinn. eine phase ein decision yaml file." A 1029-bullet `decisions.md` produces ~186 phase-YAMLs (one per phase heading), not 1029 individual files. File-per-bullet creates filesystem flood, harder to review, harder to grep semantically (you want "all decisions for p0161" → open one file, not glob).

**How to apply:**
- `log-decision` appends a new entry to the phase's existing decision YAML (or creates the YAML if first decision for that phase).
- Migration v1→v2: one YAML per `## p{NN}:` heading in decisions.md, all bullets become entries in its `decisions:` list.
- Schema must support: top-level `phase:` (or `run:`) + `decisions:` array. `over:` is optional (migrated data often lacks an explicit alternative).
- The plugin v2.0 we just shipped has the WRONG shape (one-per-decision) in templates/decision.schema.json + skills/log-decision + skills/update-project — needs a v2.0.1 follow-up to correct, but agent-smith migrates with the CORRECTED shape now.

Related: [[feedback_questions_as_text]] — present clarifications as text, not dialog. [[feedback_bias_to_defaults_not_questions]] — once the operator says "proceed", make defaults instead of stacking more questions.
