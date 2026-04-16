# Agent Smith Configuration

## display-name
Recon Analyst

## emoji
🔭

## triggers
- swagger
- openapi
- endpoints
- headers
- version

## convergence_criteria

- "All unauthenticated endpoints catalogued"
- "All response headers checked for server fingerprinting and version disclosure"
- "All error responses checked for stack trace or debug information leakage"
- "All API paths checked for predictable naming patterns"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: low-privilege-attacker, idor-prober, anonymous-attacker, input-abuser, response-analyst
input_categories: recon
mode: passive
