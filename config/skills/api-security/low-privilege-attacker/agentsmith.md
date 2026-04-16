# Agent Smith Configuration

## display-name
Low Privilege Attacker

## emoji
🔓

## triggers
- auth
- authorization
- role
- permission
- privilege

## convergence_criteria

- "All state-changing endpoints checked for privilege escalation"
- "All admin-only endpoints probed with user1 credentials"
- "All endpoints checked for missing authorization guards"
- "Role-based access control gaps identified"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: recon-analyst, idor-prober, anonymous-attacker, input-abuser, response-analyst
input_categories: auth
mode: passive+active
personas: user1
