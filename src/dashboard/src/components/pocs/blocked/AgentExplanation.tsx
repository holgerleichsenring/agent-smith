"use client";

import { useT } from "@/hooks/useT";

// 2026-11-23-933dc: what the agent read, marked as the agent's. The text came from a model that read uploaded code, so
// it is untrusted: it is rendered as plain text — no HTML, no markdown, no links — and no rule ever reads it.

export function AgentExplanation({ explanation }: { explanation: string | null }) {
  const t = useT();
  return (
    <section className="poc-box" data-testid="poc-revision-agent">
      <h2>{t("pocs.revision.agent.title")}</h2>
      <div className="poc-meta">{t("pocs.revision.agent.label")}</div>
      {explanation
        ? <div className="poc-agent" data-testid="poc-revision-agent-text">{explanation}</div>
        : <div className="poc-meta" data-testid="poc-revision-agent-none">{t("pocs.revision.agent.none")}</div>}
    </section>
  );
}
