# Agent Smith Configuration

## display-name
DBA

## emoji
🗄️

## triggers
- database
- schema-change
- migration
- query-performance
- data-integrity
- indexing

## convergence_criteria

- "Schema changes are backward compatible or migration is planned"
- "Query performance is acceptable"
- "Data integrity is maintained"

## orchestration
role: contributor
output: artifact
runs_after: lead
runs_before: gate
parallel_with: backend-developer, frontend-developer, devops, product-owner
input_categories: 
