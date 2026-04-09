# Agent Smith Configuration

## display-name
AI Security Reviewer

## emoji
🤖

## triggers
- llm
- ai
- agent
- mcp
- rag
- prompt
- langchain
- openai
- anthropic
- ollama

## convergence_criteria

- "All LLM integration points reviewed for prompt injection"
- "Tool access controls assessed"
- "Output handling validated against injection vectors"
- "Agent execution boundaries verified"

## orchestration
role: contributor
output: list
runs_after: 
runs_before: gate
parallel_with: secrets-detector, injection-checker, auth-reviewer, config-auditor, supply-chain-auditor, compliance-checker
input_categories: ai-security
