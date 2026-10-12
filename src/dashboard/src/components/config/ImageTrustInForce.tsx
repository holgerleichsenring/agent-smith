"use client";

import type { ImageTrustBoundary } from "@/lib/configApi";

// 2026-10-12-6bfab: the boundary the NEXT stack.image decision is judged against, as the
// server reads it — never re-derived here, so the panel and the decision cannot disagree.
// Each half says where it came from: the built-in default or the operator's list, the
// operator's switch or "follows the list".

export function ImageTrustInForce({
  boundary,
  error,
  dirty,
}: {
  boundary: ImageTrustBoundary | null;
  error: Error | null;
  dirty: boolean;
}) {
  if (error)
    return (
      <p className="help" data-testid="setting-sandbox-trust-inforce-error" style={{ color: "var(--bad)" }}>
        The boundary in force could not be read: {error.message}
      </p>
    );
  if (!boundary) return <p className="help">Reading the boundary in force…</p>;
  return (
    <div className="field" data-testid="setting-sandbox-trust-inforce">
      <label>
        In force now
        {dirty && (
          <span className="help" data-testid="setting-sandbox-trust-unsaved">
            unsaved changes — this is what runs use until you save
          </span>
        )}
      </label>
      <ul className="mono" style={{ margin: 0, paddingLeft: 18 }}>
        {boundary.registries.map((r) => (
          <li key={r}>{r}</li>
        ))}
      </ul>
      <p className="help" data-testid="setting-sandbox-trust-inforce-registries">
        {boundary.registriesAreDefault
          ? "the built-in default — no registry is named"
          : "the registries named here, replacing the built-in default"}
      </p>
      <p className="help" data-testid="setting-sandbox-trust-inforce-library">
        Docker Hub official library images (node:20-bookworm) are{" "}
        <strong>{boundary.dockerHubLibrary ? "trusted" : "refused"}</strong>
        {boundary.dockerHubLibraryIsExplicit ? " — set explicitly" : " — following the registry list"}
      </p>
    </div>
  );
}
