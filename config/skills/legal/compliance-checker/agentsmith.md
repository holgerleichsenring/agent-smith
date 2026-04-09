# Agent Smith Configuration

## display-name
Compliance Checker

## emoji
🔏

## triggers
- saas-agb
- werkvertrag
- dienstleistungsvertrag
- b2c-contract

## convergence_criteria

- "DSGVO relevance determined (yes/no/unclear)"
- "AGB character determined (yes/no/unclear)"
- "All applicable §§ checked"

## orchestration
role: contributor
output: artifact
runs_after: lead
runs_before: executor
parallel_with: liability-analyst, risk-assessor
input_categories: 
