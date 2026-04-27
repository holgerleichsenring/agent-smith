# Agent Smith Configuration

## display-name
Upload Validator Reviewer

## emoji
📤

## triggers
- upload
- file
- multipart
- mime
- filename

## convergence_criteria

- "All upload handlers reviewed for content-sniffing vs header-only MIME"
- "Filename sanitization checked"
- "Server-side size limits confirmed"

## orchestration
role: contributor
output: list
runs_after:
runs_before: chain-analyst
parallel_with: auth-config-reviewer, ownership-checker
input_categories: design
mode: source-only
