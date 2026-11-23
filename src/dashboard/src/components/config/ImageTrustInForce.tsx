"use client";

import type { ImageTrustBoundary } from "@/lib/configApi";
import { useT } from "@/hooks/useT";

// 2026-10-12-6bfab: the boundary the NEXT stack.image decision is judged against, as the
// server reads it — never re-derived here, so the panel and the decision cannot disagree.
// It says where the list came from: the built-in default or the operator's list.

export function ImageTrustInForce({
  boundary,
  error,
  dirty,
}: {
  boundary: ImageTrustBoundary | null;
  error: Error | null;
  dirty: boolean;
}) {
  const t = useT();
  if (error)
    return (
      <p className="help" data-testid="setting-sandbox-trust-inforce-error" style={{ color: "var(--bad)" }}>
        {t("config.imageTrust.readFailed", { message: error.message })}
      </p>
    );
  if (!boundary) return <p className="help">{t("config.imageTrust.reading")}</p>;
  return (
    <div className="field" data-testid="setting-sandbox-trust-inforce">
      <label>
        {t("config.imageTrust.inForce")}
        {dirty && (
          <span className="help" data-testid="setting-sandbox-trust-unsaved">
            {t("config.imageTrust.unsaved")}
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
          ? t("config.imageTrust.default")
          : t("config.imageTrust.named")}
      </p>
    </div>
  );
}
