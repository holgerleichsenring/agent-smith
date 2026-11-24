import type { DeliveryProject } from "@/lib/deliveryApi";
import { useLanguage } from "@/hooks/useT";
import { NONE, formatDuration, formatRate, formatUsd, wording } from "@/lib/delivery/deliveryFormat";

// 2026-10-25-0099c: one row per project the caller may read that had work in the period.

export function DeliveryProjects({ projects }: { projects: DeliveryProject[] }) {
  const w = wording(useLanguage());
  const { t } = w;
  return (
    <section className="panel" aria-labelledby="delivery-proj" data-testid="delivery-projects">
      <div className="panel-head"><h2 id="delivery-proj">{t("delivery.projects.title")}</h2></div>
      <div className="tbl-scroll">
        <table className="narrow">
          <thead>
            <tr>
              <th>{t("delivery.col.project")}</th><th className="num">{t("delivery.col.tickets")}</th>
              <th className="num">{t("delivery.col.merged")}</th><th className="num">{t("delivery.col.rate")}</th>
              <th className="num">{t("delivery.col.costPerMerged")}</th><th className="num">{t("delivery.col.medianAttempt")}</th>
            </tr>
          </thead>
          <tbody>
            {projects.map((p) => (
              <tr key={p.project} data-testid={`delivery-project-${p.project}`}>
                <td className="mono">{p.project}</td>
                <td className="num">{p.tickets}</td>
                <td className="num">{p.merged}</td>
                <td className="num">{p.mergeRate === null ? NONE : formatRate(w, p.mergeRate)}</td>
                <td className="num">{p.costPerMergedUsd === null ? NONE : formatUsd(w, p.costPerMergedUsd)}</td>
                <td className="num">{p.medianAttemptSeconds === null ? NONE : formatDuration(w, p.medianAttemptSeconds)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
