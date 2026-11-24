"use client";

import { useState } from "react";
import { useT } from "@/hooks/useT";
import { startDeploy, type DeploymentBindingView } from "@/lib/deploymentsApi";

// 2026-11-18-3863d: which context, which environment, which version. An empty version asks the run to promote from
// the environment the template names; whether this environment promotes is the template's, so a wrong choice comes
// back as the deploy run's outcome.

type Outcome = { kind: "idle" } | { kind: "sending" } | { kind: "started"; runId: string } | { kind: "said"; text: string };

export function DeployForm({ project, ticketId, bindings, onStarted }: {
  project: string; ticketId: string; bindings: DeploymentBindingView[]; onStarted: () => void;
}) {
  const t = useT();
  const [index, setIndex] = useState(0);
  const binding = bindings[Math.min(index, bindings.length - 1)];
  const [environment, setEnvironment] = useState(binding.environments[0]?.name ?? "");
  const [version, setVersion] = useState("");
  const [outcome, setOutcome] = useState<Outcome>({ kind: "idle" });
  const chosen = binding.environments.find((e) => e.name === environment);

  const deploy = async () => {
    setOutcome({ kind: "sending" });
    try {
      const answer = await startDeploy(project, {
        context: binding.context, contextRepo: binding.contextRepo ?? null, environment,
        version: version.trim() || null, ticketId,
      });
      if (answer.kind === "started") onStarted();
      setOutcome(answer.kind === "started" ? { kind: "started", runId: answer.runId } : { kind: "said", text: answer.text });
    } catch (thrown) {
      setOutcome({ kind: "said", text: thrown instanceof Error ? thrown.message : String(thrown) });
    }
  };

  return (
    <span className="deploy-form" data-testid="deploy-form">
      <select aria-label={t("tickets.deploy.context")} value={index} data-testid="deploy-context"
        onChange={(e) => { const next = Number(e.target.value); setIndex(next); setEnvironment(bindings[next].environments[0]?.name ?? ""); }}>
        {bindings.map((b, i) => <option key={i} value={i}>{b.contextRepo ? `${b.contextRepo}/${b.context}` : b.context}</option>)}
      </select>
      <select aria-label={t("tickets.deploy.environment")} value={environment} data-testid="deploy-environment" onChange={(e) => setEnvironment(e.target.value)}>
        {binding.environments.map((e) => <option key={e.name} value={e.name}>{e.name}</option>)}
      </select>
      <input type="text" className="mono" aria-label={t("tickets.deploy.version")} placeholder={t("tickets.deploy.versionPlaceholder")} value={version}
        data-testid="deploy-version" onChange={(e) => setVersion(e.target.value)} />
      <button type="button" className="btn" onClick={() => void deploy()} disabled={outcome.kind === "sending" || !environment}
        data-testid="deploy-start">{t("tickets.deploy.start")}</button>
      {chosen && (
        <span className="cp-note" data-testid="deploy-approval">
          {chosen.approval ? t("tickets.deploy.personMerges") : t("tickets.deploy.agentMerges")}
        </span>
      )}
      {outcome.kind === "started" && <span className="cp-note" data-testid="deploy-started">{t("tickets.deploy.started", { runId: outcome.runId })}</span>}
      {outcome.kind === "said" && <span className="cp-note bad" data-testid="deploy-refused">{outcome.text}</span>}
    </span>
  );
}
