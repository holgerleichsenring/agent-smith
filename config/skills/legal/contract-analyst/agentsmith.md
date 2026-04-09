# Agent Smith Configuration

## display-name
Contract Analyst

## emoji
📑

## triggers
- nda
- werkvertrag
- dienstleistungsvertrag
- saas-agb
- kaufvertrag
- mietvertrag
- unknown

## convergence_criteria

- "All clauses have been identified and described"
- "No section of the contract is left unanalyzed"

## orchestration
role: lead
output: plan
runs_after: 
runs_before: contributor
parallel_with: 
input_categories: 
