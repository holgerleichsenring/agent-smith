"use client";

import type { UnmeasuredAccount } from "@/types/hub-events";

// 2026-10-12-6bfad: an account nobody could take — a red build, a diff that would not run.
// It judged no criterion, so it is not drawn as one: a build that did not run used to read
// as a criterion titled after the sandbox key, "0 of 1 proven". The repository is a label
// and the reason is shown verbatim, because the reason is the whole finding.

const CROSS = (
  <svg viewBox="0 0 16 16" width="14" height="14" fill="none" aria-hidden="true">
    <path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
  </svg>
);

export function UnmeasuredAccounts({ accounts }: { accounts: UnmeasuredAccount[] }) {
  return (
    <div data-testid="verify-unmeasured">
      <p className="hint" style={{ marginBottom: 6 }}>
        <b>Acceptance could not be measured</b> — no criterion was judged here, so none is
        counted as proven or failed:
      </p>
      {accounts.map((account, i) => (
        <div key={i} className="crit fail" data-testid="verify-unmeasured-account">
          <div className="c-mark">{CROSS}</div>
          <div>
            <div className="c-proof">
              in <span className="mono" data-testid="verify-unmeasured-repositories">{account.repositories}</span>
            </div>
            {account.reason && (
              <pre
                className="c-proof"
                style={{ whiteSpace: "pre-wrap", margin: 0 }}
                data-testid="verify-unmeasured-reason"
              >
                {account.reason}
              </pre>
            )}
          </div>
          <div className="c-stat">not measured</div>
        </div>
      ))}
    </div>
  );
}
