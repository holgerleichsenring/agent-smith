# Agent Smith Configuration

## display-name
Vulnerability Analyst

## emoji
🔍

## triggers
- security-scan
- code-change
- api-endpoint
- user-input

## convergence_criteria

- "All changed files have been reviewed"
- "No HIGH severity finding left unexamined"
- "Every finding has a specific code reference and attack vector"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: 
input_categories: 
