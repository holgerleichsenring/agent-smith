# Agent Smith Configuration

## display-name
Input Abuser

## emoji
💉

## triggers
- user-input
- validation
- sanitization
- file-upload

## convergence_criteria

- "All user input entry points identified"
- "All missing validation and sanitization patterns found"
- "All output encoding gaps assessed"
- "All file upload handlers checked for type/size restrictions"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: recon-analyst, low-privilege-attacker, idor-prober, response-analyst
input_categories: injection, ssrf
