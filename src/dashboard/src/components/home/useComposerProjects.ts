"use client";

import { useEffect, useState } from "react";
import type { ProjectsRead } from "@/components/dialog/DialogProjectChoice";
import { fetchSpecDialog } from "@/lib/specDialogApi";
import { mintDialogId } from "@/lib/specDialogSession";
import type { SpecDialogProject } from "@/types/spec-dialog";

// 2026-11-17-d6fba: the projects a conversation can be started on, read the way Work it out reads them — the dialog
// view of an id that holds nothing. The id is minted for this read alone and never held, so the operator's own Work
// it out conversation is not touched.

export function useComposerProjects(): { projects: SpecDialogProject[]; read: ProjectsRead } {
  const [projects, setProjects] = useState<SpecDialogProject[]>([]);
  const [read, setRead] = useState<ProjectsRead>("pending");
  useEffect(() => {
    const controller = new AbortController();
    fetchSpecDialog(mintDialogId(), controller.signal)
      .then((view) => {
        setProjects(view.projects ?? []);
        setRead("read");
      })
      .catch(() => {
        if (!controller.signal.aborted) setRead("failed");
      });
    return () => controller.abort();
  }, []);
  return { projects, read };
}
