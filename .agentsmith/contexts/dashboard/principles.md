# Agent Smith Dashboard - Coding Principles

These rules state only what this tree already enforces. They carry no Limits fence: no
structure limit has been measured for the dashboard's TypeScript.

## Language

- All text in code is English: comments, identifiers, UI strings, test names, commit messages.
- No customer, project or target names anywhere in the dashboard, its fixtures or its mocks.

## TypeScript

- The compiler runs in strict mode (`src/dashboard/tsconfig.json`); code compiles without
  loosening it.

## Tests

- Tests are vitest tests next to the code, under `src/**/*.test.ts` or `src/**/*.test.tsx`
  (`src/dashboard/vitest.config.ts`).

## Events

- Every C# event record under `src/backend/AgentSmith.Contracts/Events/` has its TypeScript
  mirror in `src/dashboard/src/types/hub-events.ts` or `system-events.ts`;
  `pnpm --dir src/dashboard run gen:hub-events` fails when one is missing.

## Packages

- pnpm only, at the version `packageManager` pins, installed with `--frozen-lockfile`; a
  dependency change commits its `pnpm-lock.yaml`.
