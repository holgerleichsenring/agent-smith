import { useT } from "@/hooks/useT";
import type { SpecDialogConfigChange } from "@/types/spec-dialog";

// 2026-11-17-f3a8c: the admin assistant's drafted configuration changes, as Apply will apply them — each entity
// with what the store holds now and what it will hold. Plain text, the way the Config studio shows a document.

export function DialogConfigChanges({ changes }: { changes: SpecDialogConfigChange[] }) {
  const t = useT();
  return (
    <ol data-testid="dialog-config-changes" className="flex flex-col gap-3">
      {changes.map((change) => (
        <li key={`${change.kind}/${change.id}`} data-testid="dialog-config-change" className="d-turn-box flex flex-col gap-1 px-3 py-2">
          <div className="dsh-body font-semibold text-ink">
            {t(change.operation === "delete" ? "dialog.config.delete" : change.before ? "dialog.config.change" : "dialog.config.create")} {change.kind}/{change.id}
          </div>
          {change.before && <Document label={t("dialog.config.now")} text={change.before} />}
          {change.operation !== "delete" && change.document && <Document label={t("dialog.config.after")} text={change.document} />}
        </li>
      ))}
    </ol>
  );
}

function Document({ label, text }: { label: string; text: string }) {
  return (
    <div>
      <div className="ec-sub">{label}</div>
      <pre className="mono dsh-body whitespace-pre-wrap break-all text-ink">{pretty(text)}</pre>
    </div>
  );
}

function pretty(text: string): string {
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}
