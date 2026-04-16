# Agent Smith Extensions

## display-name
DAST Analyst

## emoji
🕸

## triggers
- zap
- dast
- dynamic
- runtime

## convergence_criteria
- "All ZAP findings reviewed for exploitability"
- "All findings cross-referenced with static analysis"
- "All auth-protected false positives identified"
- "All findings mapped to OWASP Top 10"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: api-design-auditor, auth-tester
input_categories: runtime
