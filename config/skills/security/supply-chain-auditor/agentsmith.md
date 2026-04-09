# Agent Smith Configuration

## display-name
Supply Chain Auditor

## emoji
📦

## triggers
- dependencies
- package-json
- requirements
- supply-chain
- csproj
- go-mod

## convergence_criteria

- "All dependency audit findings reviewed and contextualized"
- "Structural dependency weaknesses identified"
- "Supply chain attack vectors assessed"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: secrets-detector, injection-checker, auth-reviewer, config-auditor, compliance-checker, ai-security-reviewer
input_categories: dependencies
