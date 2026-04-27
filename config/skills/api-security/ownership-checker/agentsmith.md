# Agent Smith Configuration

## display-name
Ownership Checker

## emoji
🛂

## triggers
- idor
- bola
- ownership
- tenant
- authorization

## convergence_criteria

- "Every state-changing handler reviewed for ownership predicate"
- "Bulk and search endpoints reviewed for tenant scope"

## orchestration
role: contributor
output: list
runs_after:
runs_before: chain-analyst
parallel_with: auth-config-reviewer, upload-validator-reviewer, idor-prober
input_categories: auth, design
mode: source-only
