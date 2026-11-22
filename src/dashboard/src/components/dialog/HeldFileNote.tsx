"use client";

import { useT } from "@/hooks/useT";

// 2026-10-09-86e1: a single picked file the conversation already holds is not sent — the composer
// says where it is and offers to send it anyway, because the operator owns that choice. (The
// server refuses a pick whose every file is held, so "anyway" is answered by the server's 409
// when nothing in it is new.)

export function HeldFileNote({ file, name, onSendAnyway, onDismiss }: {
  file: File;
  name: string;
  onSendAnyway: () => void;
  onDismiss: () => void;
}) {
  const t = useT();
  return (
    <div className="d-upload-note d-held" role="status" data-testid="dialog-composer-held">
      <span>{t("dialog.held.note", { file: file.name, name })}</span>
      <span className="d-held-actions">
        <button type="button" className="btn" data-testid="dialog-composer-held-dismiss" onClick={onDismiss}>{t("dialog.held.ok")}</button>
        <button type="button" className="btn" data-testid="dialog-composer-held-send" onClick={onSendAnyway}>{t("dialog.held.sendAnyway")}</button>
      </span>
    </div>
  );
}
