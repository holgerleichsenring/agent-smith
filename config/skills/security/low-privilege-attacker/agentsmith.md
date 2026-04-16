# Agent Smith Configuration

## display-name
Low Privilege Attacker

## emoji
🔓

## triggers
- authorization
- role-check
- permission
- access-control

## convergence_criteria

- "All code paths checked for missing authorization guards"
- "All role-based access patterns verified for enforcement"
- "All admin-only functions checked for privilege escalation"
- "Backend trust of frontend visibility identified"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: recon-analyst, idor-prober, input-abuser, response-analyst
input_categories: injection, secrets
