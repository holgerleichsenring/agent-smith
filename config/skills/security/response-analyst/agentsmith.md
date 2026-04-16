# Agent Smith Configuration

## display-name
Response Analyst

## emoji
📡

## triggers
- response
- error-handling
- exception
- data-exposure

## convergence_criteria

- "All error/exception handlers checked for information disclosure"
- "All response builders checked for over-exposed data"
- "All stack trace and debug info leakage paths identified"
- "All internal ID exposure in responses assessed"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: chain-analyst
parallel_with: recon-analyst, low-privilege-attacker, idor-prober, input-abuser
input_categories: config, compliance
