import type { DeliveryProject } from "@/lib/deliveryApi";
import { NONE, formatDuration, formatRate, formatUsd } from "@/lib/delivery/deliveryFormat";

// 2026-10-25-0099c: one row per project the caller may read that had work in the period.

export function DeliveryProjects({ projects }: { projects: DeliveryProject[] }) {
  return (
    <section className="panel" aria-labelledby="delivery-proj" data-testid="delivery-projects">
      <div className="panel-head"><h2 id="delivery-proj">By project</h2></div>
      <div className="tbl-scroll">
        <table className="narrow">
          <thead>
            <tr>
              <th>Project</th><th className="num">Tickets</th><th className="num">Merged</th><th className="num">Rate</th>
              <th className="num">Cost / merged</th><th className="num">Median attempt</th>
            </tr>
          </thead>
          <tbody>
            {projects.map((p) => (
              <tr key={p.project} data-testid={`delivery-project-${p.project}`}>
                <td className="mono">{p.project}</td>
                <td className="num">{p.tickets}</td>
                <td className="num">{p.merged}</td>
                <td className="num">{p.mergeRate === null ? NONE : formatRate(p.mergeRate)}</td>
                <td className="num">{p.costPerMergedUsd === null ? NONE : formatUsd(p.costPerMergedUsd)}</td>
                <td className="num">{p.medianAttemptSeconds === null ? NONE : formatDuration(p.medianAttemptSeconds)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
