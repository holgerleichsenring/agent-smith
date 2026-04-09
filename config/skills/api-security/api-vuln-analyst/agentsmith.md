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
