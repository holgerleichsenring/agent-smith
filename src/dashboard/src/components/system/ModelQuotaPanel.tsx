"use client";

import { useCallback, useEffect, useState } from "react";
import { fetchModelQuota, type DeploymentQuota, type ModelQuota } from "@/lib/modelQuotaApi";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

// 2026-11-22-66e4a: the Model quota section of the connection check — per model deployment, how much of the minute is
// free on this server, when it is full again, and what the provider itself last said is left. Refreshed while open.

const REFRESH_MS = 5000;

export function ModelQuotaPanel() {
  const t = useT();
  const [quota, setQuota] = useState<ModelQuota | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    try {
      setQuota(await fetchModelQuota(signal));
      setError(null);
    } catch (e) {
      if ((e as Error).name !== "AbortError") setError((e as Error).message);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    const timer = setInterval(() => void load(controller.signal), REFRESH_MS);
    return () => {
      clearInterval(timer);
      controller.abort();
    };
  }, [load]);

  return (
    <section data-testid="model-quota">
      <div className="section-head">
        <h2>{t("quota.title")}</h2>
        {quota && <span className="cnt">{quota.deployments.length}</span>}
        <span className="sh-sub">{quota ? t("quota.sub", { replica: quota.replica }) : t("quota.subPlain")}</span>
      </div>
      <div style={{ height: 14 }} />
      {error ? (
        <div className="stateline err" data-testid="model-quota-error">{t("quota.failed", { message: error })}</div>
      ) : !quota ? (
        <div className="stateline">{t("quota.loading")}</div>
      ) : quota.deployments.length === 0 ? (
        <div className="stateline" data-testid="model-quota-empty">{t("quota.none")}</div>
      ) : (
        <div className="list">
          {quota.deployments.map((d) => (
            <QuotaRow key={`${d.provider}|${d.deployment}`} quota={d} t={t} />
          ))}
        </div>
      )}
    </section>
  );
}

function QuotaRow({ quota, t }: { quota: DeploymentQuota; t: T }) {
  return (
    <div className="ecard" data-testid={`model-quota-row-${quota.deployment}`}>
      <div className="ec-top">
        <div style={{ minWidth: 0 }}>
          <div className="ec-name sans">
            {t("quota.free", { deployment: quota.deployment, free: compact(quota.freeTokens), limit: compact(quota.tokensPerMinute) })}
          </div>
          <div className="ec-sub" data-testid={`model-quota-detail-${quota.deployment}`}>
            {details(quota, t).join(" · ")}
          </div>
        </div>
      </div>
    </div>
  );
}

function details(quota: DeploymentQuota, t: T): string[] {
  const parts = [
    quota.provider,
    t("quota.requests", { free: quota.freeRequests, limit: quota.requestsPerMinute }),
    quota.secondsToFull >= 1 ? t("quota.fullIn", { seconds: Math.ceil(quota.secondsToFull) }) : t("quota.full"),
  ];
  if (quota.pausedSeconds > 0) parts.push(t("quota.paused", { seconds: Math.ceil(quota.pausedSeconds) }));
  if (quota.waiting > 0) parts.push(t("quota.waiting", { count: quota.waiting }));
  if (quota.providerRemainingTokens != null)
    parts.push(t("quota.providerSays", { tokens: compact(quota.providerRemainingTokens) }));
  return parts;
}

/** 180000 → "180k"; below a thousand as it is. */
function compact(n: number): string {
  return n >= 1000 ? `${Math.round(n / 1000)}k` : String(n);
}
