# Expectation replay eval

- model: `gpt-4.1`
- skills pin: `v4.0.0`
- generated: 2026-08-03T16:20:00.4289850+00:00
- fixtures: 1

**Aggregate:** 4/4 gold assertions matched (100 %), 0 missed, 0 hallucinated.

## synthetic-example
- [x] matched: Importing a CSV with a duplicate widget name reports that row as rejected in the import summary.
  - by draft: When a widget with a duplicate name is encountered during import, the row is rejected with a visible error or warning.
- [x] matched: The import summary's imported count equals the number of rows actually persisted.
  - by draft: The import summary accurately reflects only the number of successfully imported widgets.
- [x] matched: A CSV with only unique widget names imports exactly as before.
  - by draft: Imports with all unique names are processed without errors or rejections.
- [x] matched: Each rejected row is logged with its line number and the conflicting name.
  - by draft: Logging or reporting must occur when a duplicate name is rejected.
