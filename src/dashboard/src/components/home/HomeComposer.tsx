"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { DialogComposer } from "@/components/dialog/DialogComposer";
import type { PickLeftOut } from "@/components/dialog/referenceSelection";
import { startNewDialog } from "@/lib/specDialogSession";
import { handOver, type WorkItOutHandover } from "@/lib/workItOutHandover";
import type { ProjectsRead } from "@/components/dialog/DialogProjectChoice";
import { useComposerProjects } from "./useComposerProjects";

// 2026-11-17-d6fba: the home page's composer. It is Work it out's own composer: what is typed, the project it is about
// and the files picked beside it are handed to the Work it out page, which starts the conversation with them exactly
// as its own composer would. A fresh conversation is held first, so the one the operator left open there stays in
// their recents rather than receiving this message.

export const WORK_IT_OUT_PATH = "/spec-dialog";

export function HomeComposer() {
  const router = useRouter();
  const { projects, read } = useComposerProjects();
  return (
    <NewWorkComposer
      projects={projects.map((p) => p.name)}
      read={read}
      onStart={(handover) => {
        startNewDialog();
        handOver(handover);
        router.push(WORK_IT_OUT_PATH);
      }}
    />
  );
}

/**
 * 2026-11-17-76f1: the new-work composer itself — text, the project beside the plus, files held until send. The home
 * page hands what it collects to Work it out; Work it out's own new conversation starts it in place. One composer, so
 * the two cannot drift apart.
 */
export function NewWorkComposer({ projects, read, onStart }: {
  projects: string[]; read: ProjectsRead; onStart: (handover: WorkItOutHandover) => void;
}) {
  const [picked, setPicked] = useState("");
  const [images, setImages] = useState<File[]>([]);
  const [sets, setSets] = useState<Array<{ files: File[]; leftOut?: PickLeftOut }>>([]);
  const project = (picked && projects.includes(picked) ? picked : "") || projects[0] || "";
  const fileCount = images.length + sets.reduce((n, set) => n + set.files.length, 0);

  const send = (text: string) => {
    if (!project) return;
    onStart({ text, project, images, sets });
  };

  return (
    <section className="home-composer mock-dialog" aria-label="New work" data-testid="home-composer">
      <DialogComposer
        onSend={send}
        disabled={project === ""}
        placeholder="Describe new work, paste a ticket link, or add files"
        onAttach={(file) => setImages((held) => [...held, file])}
        onAttachSite={(files, leftOut) => setSets((held) => [...held, { files, leftOut }])}
        tools={<ProjectPick projects={projects} read={read} value={project} onPick={setPicked} />}
        actions={fileCount > 0 && (
          <span className="home-files" data-testid="home-composer-files">
            {fileCount === 1 ? "1 file goes with your message" : `${fileCount} files go with your message`}
            <button type="button" className="home-link" onClick={() => { setImages([]); setSets([]); }}
              data-testid="home-composer-files-clear">Remove</button>
          </span>
        )}
      />
    </section>
  );
}

function ProjectPick({ projects, read, value, onPick }: {
  projects: string[]; read: ProjectsRead; value: string; onPick: (project: string) => void;
}) {
  if (read === "pending") return <span className="home-dim">Reading the projects…</span>;
  if (read === "failed") return <span className="home-dim" data-testid="home-composer-projects-failed">The projects could not be read.</span>;
  if (projects.length === 0) {
    return <span className="home-dim" data-testid="home-composer-no-project">No project is configured yet. Add one in Settings to start work.</span>;
  }
  if (projects.length === 1) return <span className="home-project" data-testid="home-composer-project">{projects[0]}</span>;
  return (
    <span className="home-project-pick">
      <select className="home-project" aria-label="Project" value={value} onChange={(event) => onPick(event.target.value)}
        data-testid="home-composer-project">
        {projects.map((name) => <option key={name} value={name}>{name}</option>)}
      </select>
    </span>
  );
}
