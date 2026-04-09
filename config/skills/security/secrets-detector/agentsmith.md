# Agent Smith Configuration

## display-name
Secrets Detector

## emoji
🔑

## triggers
- config
- environment
- connection-string
- api-key
- secret
- credential
- password

## convergence_criteria

- "All configuration files reviewed"
- "All string literals in changed files checked against secret patterns"
- "No real credentials in source code"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: injection-checker, auth-reviewer, config-auditor, supply-chain-auditor, compliance-checker, ai-security-reviewer
input_categories: secrets, history
