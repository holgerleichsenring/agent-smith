"use client";

import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { RUNS_CONTROL } from "../detail/RetryUnchanged";
import { DoneByHandDialog } from "./DoneByHandDialog";

// 2026-10-14-dbcen: "Mark done by hand" for one ticket — the page's own, each unlanded ticket it waits for, or a
// page-less graph node. Hidden from a caller known to lack runs.control and on a ticket that already landed.

interface Props {
  project: string;
  ticketId: string;
  label: string;
  landed: boolean;
  onLanded: () => void;
  small?: boolean;
}

export function DoneByHandAct({ project, ticketId, label, landed, onLanded, small }: Props) {
  const lacks = useLacksPermission(RUNS_CONTROL);
  const [open, setOpen] = useState(false);
  if (lacks || landed) return null;
  return (
    <>
      <button type="button" className={small ? "btn sm" : "btn"} onClick={() => setOpen(true)}
        data-testid={`done-by-hand-${ticketId}`}>
        Mark done by hand
      </button>
      {open && (
        <DoneByHandDialog project={project} ticketId={ticketId} label={label} onClose={() => setOpen(false)} onLanded={onLanded} />
      )}
    </>
  );
}
