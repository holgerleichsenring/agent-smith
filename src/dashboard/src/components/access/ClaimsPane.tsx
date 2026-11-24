"use client";

import type { AccessDocument } from "@/lib/accessApi";
import { TextField, NumberField } from "@/components/config/formFields";
import { useT } from "@/hooks/useT";

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
  const t = useT();
  return (
    <div className="db" data-testid="access-claims">
      <TextField
        label={t("access.claims.role")}
        value={draft.roleClaim}
        onChange={(v) => onChange({ ...draft, roleClaim: v })}
        mono
        placeholder="roles"
        testId="access-claims-roleclaim"
        help={t("access.claims.roleHelp")}
      />
      <TextField
        label={t("access.claims.group")}
        value={draft.groupClaim}
        onChange={(v) => onChange({ ...draft, groupClaim: v })}
        mono
        placeholder="groups"
        testId="access-claims-groupclaim"
        help={t("access.claims.groupHelp")}
      />
      <div className="field" data-testid="access-claims-nameclaim">
        <label htmlFor="access-name-claim">
          {t("access.claims.name")}
          <span className="help">{t("access.claims.nameHelp")}</span>
        </label>
        <input id="access-name-claim" className="mono" readOnly value={nameClaim} />
      </div>
      <NumberField
        label={t("access.claims.retention")}
        value={draft.observationRetentionDays}
        onChange={(v) => onChange({ ...draft, observationRetentionDays: v ?? draft.observationRetentionDays })}
        testId="access-claims-retention"
        help={t("access.claims.retentionHelp")}
      />
    </div>
  );
}
