# Agent Smith Configuration

## display-name
DevOps

## emoji
⚙️

## triggers
- infrastructure
- ci-cd
- deployment
- configuration
- environment
- docker
- kubernetes
- monitoring

## convergence_criteria

- "No new infrastructure required, or new infrastructure is justified"
- "CI/CD impact is understood and manageable"
- "No deployment risks unaddressed"

## orchestration
role: contributor
output: artifact
runs_after: lead
runs_before: gate
parallel_with: backend-developer, frontend-developer, dba, product-owner
input_categories: 
