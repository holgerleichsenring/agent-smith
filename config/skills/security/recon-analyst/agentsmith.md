# Agent Smith Configuration

## display-name
Recon Analyst

## emoji
🔭

## triggers
- exposed-endpoint
- version-string
- debug-flag
- framework-fingerprint

## convergence_criteria

- "All exposed endpoints catalogued with their attack surface"
- "All version strings and debug flags in code identified"
- "All framework fingerprints and commented-out code reviewed"
- "All deployment configuration leaks assessed"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: low-privilege-attacker, idor-prober, input-abuser, response-analyst
input_categories: config, secrets
