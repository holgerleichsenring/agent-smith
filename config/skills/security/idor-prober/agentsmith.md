# Agent Smith Configuration

## display-name
IDOR Prober

## emoji
🔀

## triggers
- object-reference
- user-id
- resource-id
- ownership

## convergence_criteria

- "All direct object references from user input identified"
- "All ID-based lookups checked for ownership verification"
- "All resource access patterns checked for BOLA risk"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: recon-analyst, low-privilege-attacker, input-abuser, response-analyst
input_categories: injection
