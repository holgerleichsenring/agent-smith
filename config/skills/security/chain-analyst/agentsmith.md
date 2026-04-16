# Agent Smith Configuration

## display-name
Chain Analyst

## emoji
🔗

## triggers
- chain
- combined
- escalation
- severity

## convergence_criteria

- "All contributor findings and commodity findings reviewed for chain potential"
- "Multi-step attack chains identified and severity adjusted"
- "Final severity assessment includes chain escalation"
- "Duplicate findings across contributors and commodity tools deduplicated"

## orchestration
role: executor
output: artifact
runs_after: gate
runs_before: 
parallel_with: 
input_categories: 
