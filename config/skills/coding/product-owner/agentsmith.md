# Agent Smith Configuration

## display-name
Product Owner

## emoji
📋

## triggers
- scope-clarification
- acceptance-criteria
- user-impact
- feature-priority
- requirements

## convergence_criteria

- "Acceptance criteria are fully addressed"
- "No scope creep beyond ticket requirements"
- "User-facing behavior is clearly defined"

## orchestration
role: contributor
output: artifact
runs_after: lead
runs_before: gate
parallel_with: backend-developer, frontend-developer, dba, devops
input_categories: 
