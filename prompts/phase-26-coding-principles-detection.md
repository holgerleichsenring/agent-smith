# Phase 26: Coding Principles Detection & Adaptation

## Goal

When Agent Smith works on a foreign codebase, detect existing coding style and
conventions rather than imposing defaults. Follow existing patterns if consistent;
suggest improvements if messy.

## Approach

During bootstrap (Phase 22), run additional analysis that extracts conventions:

### What to Detect

**Naming:** class/variable/file/test naming conventions (PascalCase, snake_case, etc.)

**Code style:** indentation (tabs/spaces, 2/4), lint config (.eslintrc, .editorconfig,
pyproject.toml [tool.ruff], .csharpierrc), formatter (prettier, black, gofmt).

**Architecture patterns:** DI style, error handling (exceptions/Result/error codes),
test patterns (AAA/BDD), import organization.

**Quality indicators:** average method length, test presence, consistency score.

### Output

Add `quality.detected-style` to generated `.context.yaml`:

```yaml
quality:
  detected-style:
    naming: snake_case
    indentation: { type: spaces, size: 4 }
    formatter: black
    linter: ruff
  quality-score: medium      # low/medium/high
  recommendation: "follow-existing"  # or "suggest-improvements"
```

When `quality-score` is `low`:
1. Follow whatever pattern exists in the specific file being modified
2. Note in PR: "This codebase would benefit from [specific improvement]"
3. Do NOT refactor unrelated code

### Implementation

Extension of Phase 22 bootstrap:
1. Phase 22 detector finds key files
2. Phase 22 generator creates `.context.yaml`
3. **New**: Quality analyzer reads 10-20 code files (largest)
4. **New**: LLM call: "Analyze these code samples and extract conventions"
5. **New**: Merge detected quality into `.context.yaml`

One additional LLM call (~3,000 tokens) during bootstrap. Zero ongoing cost.

## Architecture

- `IQualityAnalyzer` in Contracts
- `QualityAnalyzer` in Infrastructure
- Runs as part of bootstrap, not part of ticket pipeline
- Output merged into `.context.yaml` quality section

## Definition of Done

- [ ] Detect naming conventions from code samples
- [ ] Detect formatting tools and config
- [ ] Detect test patterns
- [ ] Quality score assessment
- [ ] Generated quality section in `.context.yaml`
- [ ] Agent follows detected conventions when writing code
- [ ] Works for .NET, Python, TypeScript
- [ ] Unit tests for convention detection logic
