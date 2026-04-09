# Agent Smith Configuration

## display-name
Backend Developer

## emoji
👨‍💻

## triggers
- implementation
- bug-fix
- refactoring
- performance
- api-endpoint
- business-logic

## convergence_criteria

- "Implementation approach is clear and feasible"
- "No open questions about code structure"

## orchestration
role: contributor
output: artifact
runs_after: lead
runs_before: gate
parallel_with: frontend-developer, dba, devops, product-owner
input_categories: 
