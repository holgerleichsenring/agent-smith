# Agent Smith Configuration

## display-name
Auth Reviewer

## emoji
🔐

## triggers
- authentication
- authorization
- oauth
- jwt
- session
- login
- token

## convergence_criteria

- "All auth-related code paths reviewed"
- "JWT validation completeness verified"
- "Session security flags checked"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: secrets-detector, injection-checker, config-auditor, supply-chain-auditor, compliance-checker, ai-security-reviewer
input_categories: secrets, injection
