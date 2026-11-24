"use client";

import { useLanguage, useT } from "@/hooks/useT";
import type { PocExtended, PocLifetime } from "@/lib/pocsApi";
import { pocDate } from "../pocDate";

// 2026-11-23-c81ee: when the PoC expires, how many days are left, how many of the owner's extensions are used, and
// Extend when the server says the caller may. Extend is a run; its answer names the new expiry.

export function LifetimePanel({ lifetime, busy, extended, onExtend }: {
  lifetime: PocLifetime; busy: boolean; extended: PocExtended | null; onExtend: () => void;
}) {
  const t = useT();
  const language = useLanguage();
  const date = pocDate(lifetime.expiresAt, language);
  return (
    <section className="poc-box poc-lifetime" data-testid="poc-detail-lifetime" data-expired={lifetime.expired}>
      <h2>{t("pocs.detail.lifetime.title")}</h2>
      <p data-testid="poc-detail-lifetime-expires">
        {lifetime.expired ? t("pocs.detail.lifetime.expiredOn", { date }) : t("pocs.detail.lifetime.expiresOn", { date })}
        {!lifetime.expired && (
          <>{" · "}<span data-testid="poc-detail-lifetime-days">{t("pocs.detail.lifetime.daysLeft", { count: lifetime.daysLeft })}</span></>
        )}
      </p>
      <span className="poc-meta" data-testid="poc-detail-lifetime-extensions">
        {t("pocs.detail.lifetime.extensions", { used: lifetime.extensions, count: lifetime.ownerAllowance })}
      </span>
      {!lifetime.mayExtend && lifetime.extensions >= lifetime.ownerAllowance && (
        <span className="poc-meta">{t("pocs.detail.lifetime.adminOnly")}</span>
      )}
      {extended && (
        <p className="poc-lifetime-extended" role="status" data-testid="poc-detail-extended">
          {t("pocs.detail.lifetime.extended", { date: pocDate(extended.expiresAt, language) })}
        </p>
      )}
      {lifetime.mayExtend && (
        <div className="poc-bar">
          <ExtendButton busy={busy} onExtend={onExtend} testId="poc-detail-lifetime-extend" />
        </div>
      )}
    </section>
  );
}

export function ExtendButton({ busy, onExtend, testId }: { busy: boolean; onExtend: () => void; testId: string }) {
  const t = useT();
  return (
    <button type="button" className="btn" disabled={busy} data-testid={testId} onClick={onExtend}>
      {busy ? t("pocs.detail.lifetime.extending") : t("pocs.detail.lifetime.extend")}
    </button>
  );
}
