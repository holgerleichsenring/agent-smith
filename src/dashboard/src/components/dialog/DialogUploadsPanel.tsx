"use client";

import type { ReactNode } from "react";
import { useDialogUploads, momentOf, type DialogUpload } from "@/hooks/useDialogUploads";
import { specDialogImageUrl } from "@/lib/specDialogApi";
import type { FiledWork, SpecDialogSession } from "@/types/spec-dialog";
import { AuthedImage } from "./AuthedImage";
import { ConfirmDialog } from "./ConfirmDialog";
import { sizeOf } from "./referenceSelection";
import { UploadSetRow } from "./UploadSetRow";

// 2026-10-08-e8b9h: everything the operator handed this conversation, in one place — every set
// and image, oldest first, with what they hold against the conversation's byte cap. The
// transcript only places each at its moment, so an upload made twenty turns ago was nowhere to be
// found. An upload an approval cites cannot be removed — the run of the filed ticket builds
// against it — and says so instead of offering a Remove that would be refused. After an approval
// every set is cited, so a conversation approved near the cap takes no more uploads; the usage
// line is where the operator sees that.
// 2026-10-09-86e1: and a set opens to its files — a tree, each file previewed or removed on its
// own. `focusSetId` is the upload a transcript chip asked for.

export function DialogUploadsPanel({
  session,
  work,
  dialogId,
  onRefresh,
  focusSetId = null,
}: {
  session: SpecDialogSession;
  work: FiledWork | null;
  dialogId: string | null;
  onRefresh: () => Promise<void>;
  focusSetId?: string | null;
}) {
  const { remove, removeFile, note, dialog } = useDialogUploads(dialogId, onRefresh);
  const used = session.uploadBytes ?? 0;
  const cap = session.uploadCapBytes ?? 0;
  return (
    <div data-testid="dialog-uploads">
      {cap > 0 && (
        <p className="ec-sub mb-2" data-testid="dialog-uploads-usage">
          {`${sizeOf(used)} of ${sizeOf(cap)} used, ${sizeOf(Math.max(0, cap - used))} left`}
        </p>
      )}
      {notSeen(session) && (
        <p className="ec-sub mb-2" data-testid="dialog-uploads-not-seen">{notSeen(session)}</p>
      )}
      <ul className="flex flex-col gap-2">
        {uploadsOf(session).map((upload) => (
          <UploadRow
            key={upload.kind === "set" ? upload.set.setId : `image-${upload.image.id}`}
            upload={upload}
            approved={!!work?.approved}
            onRemove={() => void remove(upload)}
            body={(actions) => upload.kind === "set" ? (
              <UploadSetRow set={upload.set} dialogId={dialogId} focused={upload.set.setId === focusSetId}
                actions={actions} onRemoveFile={(path) => void removeFile(upload.set, path)} />
            ) : (
              <div className="flex items-center gap-2.5 px-3 py-2"><ImageSummary upload={upload} />{actions}</div>
            )}
          />
        ))}
      </ul>
      {note && (
        <p role="alert" data-testid="dialog-uploads-note" className="d-upload-note bad">
          {note}
        </p>
      )}
      <ConfirmDialog {...dialog} />
    </div>
  );
}

function UploadRow({ upload, approved, onRemove, body }: {
  upload: DialogUpload; approved: boolean; onRemove: () => void; body: (actions: ReactNode) => ReactNode;
}) {
  const cited = upload.kind === "set" ? !!upload.set.cited : !!upload.image.cited;
  const id = upload.kind === "set" ? upload.set.setId : String(upload.image.id);
  return (
    <li data-testid={`dialog-upload-${id}`} className="ecard inert">
      {body(cited ? (
        <span className="ec-sub" data-testid={`dialog-upload-cited-${id}`}>cited by an approval</span>
      ) : (
        <button type="button" className="btn" data-testid={`dialog-upload-remove-${id}`} onClick={onRemove}>
          Remove
        </button>
      ))}
      {/* The approval froze the list it cites — sets and, since 2026-10-08-e8b9k, images; one
          uploaded later reaches a run only when the ticket is approved again. */}
      {!cited && approved && (
        <p className="ec-sub px-3 pb-2" data-testid={`dialog-upload-later-${id}`}>
          not in the approval — a run carries it after the ticket is approved again
        </p>
      )}
    </li>
  );
}

function ImageSummary({ upload }: { upload: Extract<DialogUpload, { kind: "image" }> }) {
  const { image } = upload;
  return (
    <>
      <AuthedImage path={specDialogImageUrl(image.id)} alt="Attached by you" className="max-h-12 max-w-24" />
      <span className="ec-sub min-w-0 flex-1">{momentOf(image.at)}</span>
      {image.bytes != null && <span className="ec-sub">{sizeOf(image.bytes)}</span>}
    </>
  );
}

/** 2026-10-08-e8b9j: said when the conversation's model cannot see what was attached — the
 *  model is told already; the person attaching a screenshot is the one who would wait. */
export function notSeen(session: SpecDialogSession): string | null {
  const all = session.imageCount ?? 0;
  if (session.seesImages === false && all > 0)
    return `This project's model does not see images; ${all} stored image(s) are not shown to it.`;
  const inSets = all - (session.images?.length ?? 0);
  if (session.seesUploadedImages === false && inSets > 0)
    return `This project's model is not shown images inside uploads; ${inSets} image file(s) in sets are not shown to it.`;
  return null;
}

/** Every upload of the conversation, oldest first. */
export function uploadsOf(session: SpecDialogSession): DialogUpload[] {
  const sets: DialogUpload[] = (session.references ?? []).map((set) => ({ kind: "set", set }));
  const images: DialogUpload[] = (session.images ?? []).map((image) => ({ kind: "image", image }));
  return [...sets, ...images].sort((a, b) => atOf(a).localeCompare(atOf(b)));
}

function atOf(upload: DialogUpload): string {
  return new Date(upload.kind === "set" ? upload.set.at : upload.image.at).toISOString();
}
