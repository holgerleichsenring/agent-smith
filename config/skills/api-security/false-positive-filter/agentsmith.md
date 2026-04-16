# Agent Smith Configuration

## display-name
False Positive Filter

## emoji
🧹

## triggers
- always_include

## convergence_criteria

- "Every finding reviewed against exclusion criteria and confidence threshold (≥7)"
- "Nuclei and ZAP heuristics applied to all DAST findings"
- "Duplicate findings deduplicated — highest confidence entry retained"
- "No finding discarded without stated reason"
- "Retained count and filtered count documented"

## orchestration
role: gate
output: list
runs_after: contributor
runs_before: executor
parallel_with: 
input_categories: auth, design, runtime
