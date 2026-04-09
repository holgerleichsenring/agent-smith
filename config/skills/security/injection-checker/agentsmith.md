# Agent Smith Configuration

## display-name
Injection Checker

## emoji
💉

## triggers
- database
- sql
- command
- process
- shell
- ldap
- xpath
- query

## convergence_criteria

- "All database access code reviewed"
- "All process/command execution reviewed"
- "No string concatenation in queries without justification"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: secrets-detector, auth-reviewer, config-auditor, supply-chain-auditor, compliance-checker, ai-security-reviewer
input_categories: injection, ssrf
