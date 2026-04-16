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
runs_after: recon-analyst, low-privilege-attacker, idor-prober, input-abuser, response-analyst, vuln-analyst, auth-reviewer, injection-checker, secrets-detector, false-positive-filter, config-auditor, supply-chain-auditor, compliance-checker, ai-security-reviewer
runs_before: 
parallel_with: 
input_categories: 
