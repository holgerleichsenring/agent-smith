"use client";

import type { AccessDocument } from "@/lib/accessApi";
import { TextField, NumberField } from "@/components/config/formFields";

// 2026-08-26-7a51: the claim names, as their own pane.
//
// They are here because a save is a WHOLE-document write: the server binds the body onto a
// fresh model, so a people-only surface would silently reset the role claim to its default
// and empty every directory-derived role on an installation whose directory nests them.
// Rendering them is what makes round-tripping them honest rather than invisible.
//
// The name claim is NOT here: it binds into the token pipeline at startup, so it is
// bootstrap and changing it costs a restart. It is shown, and it is what the warning above
// the panes is about.

export function ClaimsPane({
  draft,
  nameClaim,
  onChange,
}: {
  draft: AccessDocument;
  nameClaim: string;
  onChange: (next: AccessDocument) => void;
}) {
  return (
    <div className="db" data-testid="access-claims">
      <TextField
        label="Role claim"
        value={draft.roleClaim}
        onChange={(v) => onChange({ ...draft, roleClaim: v })}
        mono
        placeholder="roles"
        testId="access-claims-roleclaim"
        help="the claim role NAMES are read out of, verbatim"
      />
      <TextField
        label="Group claim"
        value={draft.groupClaim}
        onChange={(v) => onChange({ ...draft, groupClaim: v })}
        mono
        placeholder="groups"
        testId="access-claims-groupclaim"
        help="the claim group values are read out of"
      />
      <div className="field" data-testid="access-claims-nameclaim">
        <label htmlFor="access-name-claim">
          Name claim
          <span className="help">bootstrap — changing it costs a restart</span>
        </label>
        <input id="access-name-claim" className="mono" readOnly value={nameClaim} />
      </div>
      <NumberField
        label="Observation retention (days)"
        value={draft.observationRetentionDays}
        onChange={(v) => onChange({ ...draft, observationRetentionDays: v ?? draft.observationRetentionDays })}
        testId="access-claims-retention"
        help="how long a caller stays on the People pane after their last request"
      />
    </div>
  );
}
