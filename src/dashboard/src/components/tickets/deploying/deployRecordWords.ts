import type { DeployRecordView } from "@/lib/deploymentsApi";
import type { T } from "@/lib/i18n/translate";

// 2026-11-18-3863f: a deploy record in words — what went where, and where it stands now.

export function deployHeadline(t: T, r: DeployRecordView): string {
  const how = r.delivery === "pipeline_trigger" ? t("tickets.deploy.pipelineRun") : t("tickets.deploy.pullRequest");
  return t("tickets.deploy.headline", { context: r.context, version: r.version, environment: r.environment, how, repo: r.repo });
}

const PR_STATE: Record<string, "tickets.deploy.pr.opened" | "tickets.deploy.pr.open" | "tickets.deploy.pr.merged" | "tickets.deploy.pr.closed"> = {
  opened: "tickets.deploy.pr.opened", open: "tickets.deploy.pr.open", merged: "tickets.deploy.pr.merged", closed: "tickets.deploy.pr.closed",
};

const CI_STATE: Record<string, "tickets.deploy.ci.red" | "tickets.deploy.ci.green" | "tickets.deploy.ci.none" | "tickets.deploy.ci.running"> = {
  red: "tickets.deploy.ci.red", green: "tickets.deploy.ci.green", none: "tickets.deploy.ci.none", running: "tickets.deploy.ci.running",
};

export function deployStanding(t: T, r: DeployRecordView): string {
  const ci = r.ciState === "red" ? t("tickets.deploy.failed") : r.ciState === "green" ? t("tickets.deploy.ci.green")
    : r.ciState === "none" ? t("tickets.deploy.noCi") : t("tickets.deploy.ci.running");
  const pr = r.pullRequestState ?? "opened";
  const where = r.delivery === "pipeline_trigger" ? ci
    : t("tickets.deploy.prStanding", {
      pr: PR_STATE[pr] ? t(PR_STATE[pr]) : pr,
      ci: CI_STATE[r.ciState] ? t(CI_STATE[r.ciState]) : r.ciState,
    });
  return r.settledAt ? t("tickets.deploy.settled", { where }) : t("tickets.deploy.followed", { where });
}

export function deployTone(r: DeployRecordView): "ok" | "bad" | "run" {
  if (r.ciState === "red" || r.pullRequestState === "closed") return "bad";
  return r.settledAt ? "ok" : "run";
}
