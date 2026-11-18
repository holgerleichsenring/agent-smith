import type { DeployRecordView } from "@/lib/deploymentsApi";

// 2026-11-18-3863f: a deploy record in words — what went where, and where it stands now.

export function deployHeadline(r: DeployRecordView): string {
  const how = r.delivery === "pipeline_trigger" ? "pipeline run" : "pull request";
  return `${r.context} ${r.version} to ${r.environment} · ${how} on ${r.repo}`;
}

export function deployStanding(r: DeployRecordView): string {
  const ci = r.ciState === "red" ? "failed" : r.ciState === "green" ? "green" : r.ciState === "none" ? "no CI" : "running";
  const where = r.delivery === "pipeline_trigger" ? ci
    : `${r.pullRequestState ?? "opened"} · CI ${r.ciState === "red" ? "red" : r.ciState}`;
  return r.settledAt ? `${where} · settled` : `${where} · followed`;
}

export function deployTone(r: DeployRecordView): "ok" | "bad" | "run" {
  if (r.ciState === "red" || r.pullRequestState === "closed") return "bad";
  return r.settledAt ? "ok" : "run";
}
