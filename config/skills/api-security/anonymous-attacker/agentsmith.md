# Agent Smith Configuration

## display-name
Anonymous Attacker

## emoji
👤

## triggers
- anonymous
- unauthenticated
- public
- rate-limit
- brute-force

## convergence_criteria

- "All unauthenticated endpoints tested for abuse potential"
- "Rate limiting presence checked on auth and data endpoints"
- "Token entropy and predictability assessed"
- "Resource exhaustion vectors identified"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: recon-analyst, low-privilege-attacker, idor-prober, input-abuser, response-analyst
input_categories: auth, design
mode: passive+active
personas:
