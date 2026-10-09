"use client";

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
  return (
    <div className="d-upload-note d-held" role="status" data-testid="dialog-composer-held">
      <span>{`${file.name} is already in this conversation, in '${name}'. It was not sent.`}</span>
      <span className="d-held-actions">
        <button type="button" className="btn" data-testid="dialog-composer-held-dismiss" onClick={onDismiss}>OK</button>
        <button type="button" className="btn" data-testid="dialog-composer-held-send" onClick={onSendAnyway}>Send anyway</button>
      </span>
    </div>
  );
}
