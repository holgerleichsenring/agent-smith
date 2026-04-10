# Agent Smith Extensions

## display-name
DAST False Positive Filter

## emoji
🚫

## triggers
- zap-filter
- dast-fp
- false-positive

## convergence_criteria
- "All Low/FP confidence findings discarded with reason"
- "All known FP patterns checked against findings"
- "No finding discarded without specific reason"

## orchestration
role: gate
output: list
runs_after: contributor
runs_before: executor
parallel_with: false-positive-filter
input_categories: runtime
