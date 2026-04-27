# Agent Smith Configuration

## display-name
Auth Config Reviewer

## emoji
🔐

## triggers
- auth
- jwt
- cors
- middleware
- security-headers

## convergence_criteria

- "All auth bootstrap files reviewed for disabled validations"
- "Middleware ordering checked end-to-end"
- "CORS configuration assessed against credentialed flows"

## orchestration
role: contributor
output: list
runs_after:
runs_before: chain-analyst
parallel_with: ownership-checker, upload-validator-reviewer, auth-tester
input_categories: auth
mode: source-only
