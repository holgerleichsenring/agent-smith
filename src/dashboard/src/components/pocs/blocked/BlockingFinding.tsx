"use client";

import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import type { PocScanFinding } from "@/lib/pocsApi";

// 2026-11-23-933dc: one finding that stops a revision — what it is, where, the masked line, how to fix it and the other
// names it goes by. Every value is the server's text, shown as text.

const KIND_LABEL: Readonly<Record<string, TextKey>> = {
  secret: "pocs.revision.kind.secret",
  vulnerability: "pocs.revision.kind.vulnerability",
  note: "pocs.revision.kind.note",
};

export function findingKindLabel(kind: string): TextKey | null {
  return KIND_LABEL[kind] ?? null;
}

export function findingPlace(finding: PocScanFinding): string {
  return finding.line != null ? `${finding.file}:${finding.line}` : finding.file;
}

export function BlockingFinding({ finding }: { finding: PocScanFinding }) {
  const t = useT();
  const kind = findingKindLabel(finding.kind);
  return (
    <article className="poc-find" data-testid={`poc-finding-${finding.id}`} data-kind={finding.kind}>
      <div className="poc-find-head">
        <span className="poc-sev">{t("pocs.revision.blocks")}</span>
        <span className="poc-kind">{kind ? t(kind) : finding.kind}</span>
        <h2>{finding.title}</h2>
        <span className="poc-place mono">{findingPlace(finding)}</span>
      </div>
      <div className="poc-find-body">
        {finding.detail && <div>{finding.detail}</div>}
        {finding.maskedSnippet && <pre className="poc-code" data-testid={`poc-finding-${finding.id}-snippet`}>{finding.maskedSnippet}</pre>}
        {finding.fix && (
          <div className="poc-fix" data-testid={`poc-finding-${finding.id}-fix`}>
            <b>{t("pocs.revision.fix")}</b> {finding.fix}
          </div>
        )}
        {finding.aliases.length > 0 && (
          <div className="poc-meta" data-testid={`poc-finding-${finding.id}-aliases`}>
            {t("pocs.revision.aliases")} <span className="mono">{finding.aliases.join(", ")}</span>
          </div>
        )}
        <div className="poc-meta mono">{finding.ruleId}</div>
      </div>
    </article>
  );
}
