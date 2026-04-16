# Agent Smith Configuration

## display-name
Input Abuser

## emoji
💉

## triggers
- input
- validation
- injection
- upload
- payload

## convergence_criteria

- "All request body schemas checked for missing validation constraints"
- "All file upload endpoints assessed for type/size restrictions"
- "All string parameters checked for injection entry points"
- "All content-type handling checked for bypass potential"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: recon-analyst, low-privilege-attacker, idor-prober, anonymous-attacker, response-analyst
input_categories: design, runtime
mode: passive+active
personas: user1
