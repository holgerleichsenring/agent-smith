# Agent Smith Configuration

## display-name
False Positive Filter

## emoji
🧹

## triggers
- security-scan
- always_include

## convergence_criteria

- "Every finding has been reviewed for false positive indicators"
- "No finding with confidence < 8 remains"
- "Filtered count and reasons documented"

## orchestration
role: gate
output: list
runs_after: contributor
runs_before: executor
parallel_with: 
input_categories: *
