# Agent Smith Configuration

## display-name
False Positive Filter

## emoji
🧹

## triggers
- always_include

## convergence_criteria

- "Every finding has been reviewed for false positive indicators"
- "No finding with confidence < 7 remains"
- "Nuclei-specific heuristics applied to all scanner findings"
- "Duplicate findings deduplicated, highest confidence entry retained"
- "Filtered count and reasons documented"

## orchestration
role: gate
output: list
runs_after: contributor
runs_before: executor
parallel_with: dast-false-positive-filter
input_categories: auth, design, runtime
