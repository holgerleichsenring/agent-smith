# Agent Smith Configuration

## display-name
IDOR Prober

## emoji
🔀

## triggers
- id
- bola
- idor
- object-reference
- sequential

## convergence_criteria

- "All ID-based path parameters checked for IDOR/BOLA risk"
- "All cross-user resource access patterns identified"
- "All sequential/guessable ID patterns flagged"
- "All ownership checks (or lack thereof) documented"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: recon-analyst, low-privilege-attacker, anonymous-attacker, input-abuser, response-analyst
input_categories: auth, design
mode: passive+active
personas: user1, user2
