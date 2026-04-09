# Agent Smith Configuration

## display-name
Liability Analyst

## emoji
⚖️

## triggers
- werkvertrag
- dienstleistungsvertrag
- saas-agb
- high-value-contract

## convergence_criteria

- "All liability-related clauses identified"
- "Legal validity assessed for each exclusion clause"

## orchestration
role: contributor
output: artifact
runs_after: lead
runs_before: executor
parallel_with: compliance-checker, risk-assessor
input_categories: 
