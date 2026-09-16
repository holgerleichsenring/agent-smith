"use client";

import type {
  SpecDialogFilingPush,
  SpecDialogProject,
  SpecDialogProposalPush,
  SpecDialogSession,
} from "@/types/spec-dialog";
import { DialogFiledPanel } from "./DialogFiledPanel";
import { DialogProposalPanel } from "./DialogProposalPanel";
import { DialogScopePanel } from "./DialogScopePanel";

// 2026-09-15-6d9c: ONE column that changes, never three panels stacked on each other.
//
//     Scope  →  Proposal  →  What was filed
//
// It shows what the conversation is currently about: what the agent may read until it has
// proposed something, the proposal while it is being decided, and the tickets that exist
// once it has been. A superseding proposal moves it back a step — the hook clears the
// filing — because what is being decided now outranks what was decided before.

export function DialogColumn({
  session,
  projects,
  proposal,
  filed,
}: {
  session: SpecDialogSession | null;
  projects: SpecDialogProject[];
  proposal: SpecDialogProposalPush | null;
  filed: SpecDialogFilingPush | null;
}) {
  if (filed) return <DialogFiledPanel filed={filed} />;
  if (proposal) return <DialogProposalPanel proposal={proposal} />;
  return <DialogScopePanel session={session} projects={projects} />;
}
