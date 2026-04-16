# Agent Smith Configuration

## display-name
Response Analyst

## emoji
📡

## triggers
- response
- schema
- data-exposure
- pii
- over-exposure

## convergence_criteria

- "All response schemas checked for over-exposed data"
- "User1 vs admin response differences analyzed"
- "All PII fields identified in responses"
- "Error responses checked for information leakage"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: recon-analyst, low-privilege-attacker, idor-prober, anonymous-attacker, input-abuser
input_categories: design
mode: passive+active
personas: user1, admin
