# Agent Smith Configuration

## display-name
API Vulnerability Analyst

## emoji
🔍

## triggers
- nuclei-findings
- vulnerability
- exploit

## convergence_criteria

- "All Nuclei findings have been evaluated against the API context"
- "Every valid finding is mapped to an OWASP API Security Top 10 category"
- "No HIGH severity finding left without a documented attack vector"
- "All findings with confidence < 7 have been discarded"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: auth-tester, api-design-auditor, dast-analyst
input_categories: auth, design, runtime 
