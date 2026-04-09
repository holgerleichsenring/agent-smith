# Agent Smith Configuration

## display-name
Compliance Checker

## emoji
📋

## triggers
- logging
- pii
- gdpr
- compliance
- privacy
- analytics
- error-handling

## convergence_criteria

- "All PII exposure vectors reviewed"
- "GDPR-relevant patterns identified and mapped to articles"
- "Data protection practices assessed"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: secrets-detector, injection-checker, auth-reviewer, config-auditor, supply-chain-auditor, ai-security-reviewer
input_categories: compliance
