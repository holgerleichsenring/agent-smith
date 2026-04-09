# Agent Smith Configuration

## display-name
Tester

## emoji
🧪

## triggers
- testing
- test-coverage
- edge-cases
- regression
- integration-test
- quality

## convergence_criteria

- "Test strategy covers critical paths"
- "Edge cases are identified and addressed"
- "No high-risk areas left untested"

## orchestration
role: gate
output: verdict
runs_after: executor
runs_before: 
parallel_with: 
input_categories: 
