# Agent Smith Configuration

## display-name
Config Auditor

## emoji
⚙️

## triggers
- dockerfile
- kubernetes
- terraform
- ci-cd
- configuration
- docker-compose
- nginx
- github-actions

## convergence_criteria

- "All infrastructure configuration files reviewed"
- "CI/CD pipeline security assessed"
- "No critical misconfigurations unaddressed"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: secrets-detector, injection-checker, auth-reviewer, supply-chain-auditor, compliance-checker, ai-security-reviewer
input_categories: config
