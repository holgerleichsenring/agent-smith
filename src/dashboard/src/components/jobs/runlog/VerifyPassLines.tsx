"use client";

import type { VerifyPassRecordedEvent, VerifyRepositoryPass } from "@/types/hub-events";

// 2026-11-17-d6fbc: a verify pass under its "Build and test" line, from the pass's own data: one line per repository
// with the stages it ran, and the contexts whose verify steps were derived from files that changed since as ONE muted
// line, their files behind a disclosure. The step's prose stays its result message for the tracker and the CLI.

const PASSED = "passed";

/** "install · build · test", a skipped stage marked, a red one with its exit code. */
export function stageWords(repo: VerifyRepositoryPass): string {
  return repo.stages
    .map((s) => (s.skipped ? `${s.stage} skipped` : s.exitCode !== 0 ? `${s.stage} exited ${s.exitCode}` : s.stage))
    .join(" · ");
}

/** "2 contexts derived their verify steps from files that changed since; the stages ran as declared". */
export function staleWords(count: number): string {
  const contexts = count === 1 ? "1 context" : `${count} contexts`;
  return `${contexts} derived ${count === 1 ? "its" : "their"} verify steps from files that changed since; the stages ran as declared`;
}

export function VerifyPassLines({ pass }: { pass: VerifyPassRecordedEvent }) {
  const stale = pass.staleDerivations ?? [];
  return (
    <div className="rl-pass" data-testid="verify-pass">
      {(pass.repositories ?? []).map((repo) => {
        const ok = repo.result === PASSED;
        return (
          <div key={repo.repository} className={`rl-pass-repo ${ok ? "done" : "bad"}`} data-testid="verify-pass-repo">
            <span className="rl-pass-mark" aria-label={ok ? "passed" : "failed"}>{ok ? "✓" : "✗"}</span>
            <span className="rl-pass-name">{repo.repository}</span>
            <span className="rl-dim">{stageWords(repo)}</span>
          </div>
        );
      })}
      {stale.length > 0 && (
        <details className="rl-pass-stale" data-testid="verify-pass-stale">
          <summary>{staleWords(stale.length)}</summary>
          {stale.map((s) => (
            <div key={`${s.repository}/${s.context}`} className="rl-dim" data-testid="verify-pass-stale-context">
              {s.repository} / {s.context}: {s.files.join(", ")}
            </div>
          ))}
        </details>
      )}
    </div>
  );
}
