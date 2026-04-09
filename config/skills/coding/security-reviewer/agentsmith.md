# Agent Smith Configuration

## display-name
Security Reviewer

## emoji
🔒

## triggers
- authentication
- authorization
- input-validation
- secrets-management
- encryption
- security-vulnerability

## convergence_criteria

- "No critical or high security vulnerabilities"
- "Input validation covers all external data"
- "Secrets are not exposed in code or configuration"

## orchestration
role: gate
output: verdict
runs_after: contributor
runs_before: executor
parallel_with: 
input_categories: 
