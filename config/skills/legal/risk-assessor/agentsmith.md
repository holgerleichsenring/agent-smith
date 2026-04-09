# Agent Smith Configuration

## display-name
Risk Assessor

## emoji
⚠️

## triggers
- nda
- werkvertrag
- dienstleistungsvertrag
- saas-agb
- kaufvertrag
- unknown

## convergence_criteria

- "Every HIGH-risk clause has a cited reason"
- "Missing clauses section is complete"
- "No clause from the analyst's list is unrated"

## orchestration
role: contributor
output: artifact
runs_after: lead
runs_before: executor
parallel_with: compliance-checker, liability-analyst
input_categories: 
