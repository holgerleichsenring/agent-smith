"use client";

import type { ConnectionRepos } from "@/lib/configApi";
import { useDateLocale, useT } from "@/hooks/useT";

// 2026-10-02-5f89c: a connection's discovery at a glance — the last success with its repo count,
// the last failed attempt with its reason beside it (a failure never erases the success), and
// Refresh now. The reason is the server's sentence.

export function DiscoveryStatus({
  testId,
  discovered,
  refreshing,
  refreshError,
  onRefresh,
}: {
  testId: string;
  discovered: ConnectionRepos | null;
  refreshing: boolean;
  refreshError: Error | null;
  onRefresh: () => void;
}) {
  const t = useT();
  const locale = useDateLocale();
  const at = (iso: string) => new Date(iso).toLocaleString(locale);
  const count = discovered?.repoCount ?? discovered?.repos.length ?? 0;
  return (
    <div className="picks" data-testid={`${testId}-status`} style={{ alignItems: "baseline", gap: 10 }}>
      {discovered?.discovering ? (
        <span className="help" data-testid={`${testId}-discovering`}>{t("config.discovery.discovering")}</span>
      ) : discovered?.discoveredAt ? (
        <span className="help" data-testid={`${testId}-success`}>
          {t("config.discovery.lastSuccess", { at: at(discovered.discoveredAt), count })}
        </span>
      ) : (
        <span className="help" data-testid={`${testId}-nosuccess`}>{t("config.discovery.noSuccess")}</span>
      )}
      {discovered?.lastError && (
        <span className="help" data-testid={`${testId}-lasterror`} style={{ color: "var(--bad)" }}>
          {discovered.lastAttemptAt
            ? t("config.discovery.lastAttemptAtFailed", { at: at(discovered.lastAttemptAt), error: discovered.lastError })
            : t("config.discovery.lastAttemptFailed", { error: discovered.lastError })}
        </span>
      )}
      <button
        type="button"
        className="pick"
        disabled={refreshing}
        onClick={onRefresh}
        data-testid={`${testId}-refresh`}
      >
        {refreshing ? t("config.discovery.refreshing") : t("config.discovery.refresh")}
      </button>
      {refreshError && (
        <span className="help" data-testid={`${testId}-refresh-error`} style={{ color: "var(--bad)" }}>
          {t("config.discovery.refreshFailed", { message: refreshError.message })}
        </span>
      )}
    </div>
  );
}
