"use client";

import { useT } from "@/hooks/useT";
import type { PocDesired } from "@/lib/pocsApi";

// 2026-11-23-c81ed: what the PoC's folder in the GitOps repo asks for — replicas, the revision, its image digest and
// the readiness probe. Argo CD carries it out; the Rollout page of each run says whether it did.

/** A digest cut to its algorithm and the first twelve characters of its hash: enough to tell two apart. */
export function shortDigest(digest: string): string {
  const at = digest.indexOf(":");
  const hash = at < 0 ? digest : digest.slice(at + 1);
  const prefix = at < 0 ? "" : digest.slice(0, at + 1);
  return hash.length > 12 ? `${prefix}${hash.slice(0, 12)}…` : digest;
}

export function DesiredState({ desired }: { desired: PocDesired }) {
  const t = useT();
  return (
    <section className="poc-box" data-testid="poc-detail-desired">
      <h2>{t("pocs.detail.desired.title")}</h2>
      <dl className="poc-kv">
        <div><dt>{t("pocs.detail.desired.replicas")}</dt><dd data-testid="poc-detail-desired-replicas">{desired.replicas}</dd></div>
        <div>
          <dt>{t("pocs.detail.desired.revision")}</dt>
          <dd data-testid="poc-detail-desired-revision">
            {desired.revision !== null ? t("pocs.rollout.revision", { revision: desired.revision }) : t("pocs.detail.none")}
          </dd>
        </div>
        <div>
          <dt>{t("pocs.detail.desired.digest")}</dt>
          <dd className="mono" title={desired.imageDigest ?? undefined} data-testid="poc-detail-desired-digest">
            {desired.imageDigest ? shortDigest(desired.imageDigest) : t("pocs.detail.none")}
          </dd>
        </div>
        <div>
          <dt>{t("pocs.detail.desired.readiness")}</dt>
          <dd className="mono" data-testid="poc-detail-desired-readiness">
            {desired.readinessPath ?? t("pocs.detail.readiness.tcp")}
          </dd>
        </div>
      </dl>
    </section>
  );
}
