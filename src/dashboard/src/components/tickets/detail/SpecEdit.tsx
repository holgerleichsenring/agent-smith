"use client";

import { useState } from "react";
import { editTicketSpec } from "@/lib/ticketSpecEditApi";
import { useTicketDetail, useTicketEdited } from "@/lib/tickets/TicketPageStore";
import type { TicketSeriesSpec } from "@/types/tickets";

// 2026-11-17-dd05c: one spec of a filed ticket, edited as its yaml. Save sends it the way an approval in the ticket's
// conversation goes — a never-worked ticket is amended (branch, record, body), a worked one keeps it for its next
// attempt — and the server's sentence is shown as it came. A spec that ran is never edited, so it has no Edit.

export function SpecEdit({ spec }: { spec: TicketSeriesSpec }) {
  const { project, ticketId } = useTicketDetail();
  const edited = useTicketEdited();
  const [yaml, setYaml] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [said, setSaid] = useState<string | null>(null);

  const save = async () => {
    if (yaml === null) return;
    setSaving(true);
    try {
      const result = await editTicketSpec(project, ticketId, spec.specId, yaml);
      setSaid(result.text);
      if (result.recorded) {
        setYaml(null);
        edited();
      }
    } catch (error) {
      setSaid(error instanceof Error ? error.message : "The edit could not be sent.");
    } finally {
      setSaving(false);
    }
  };

  if (yaml === null) {
    return (
      <div className="ro-note">
        <span data-testid="spec-edit-said">{said ?? "Edit this spec's yaml; saving records it the way an approval in the conversation does."}</span>
        <button className="btn sm" type="button" onClick={() => { setSaid(null); setYaml(spec.yaml); }} data-testid="spec-edit">Edit</button>
      </div>
    );
  }
  return (
    <div className="ro-note sp-edit">
      <textarea className="sp-yaml" value={yaml} onChange={(e) => setYaml(e.target.value)} spellCheck={false}
        aria-label={`Yaml of spec ${spec.specId}`} data-testid="spec-edit-yaml" />
      {said && <span data-testid="spec-edit-said">{said}</span>}
      <button className="btn sm" type="button" onClick={() => setYaml(null)} disabled={saving}>Cancel</button>
      <button className="btn sm" type="button" onClick={save} disabled={saving} data-testid="spec-edit-save">{saving ? "Saving…" : "Save"}</button>
    </div>
  );
}
