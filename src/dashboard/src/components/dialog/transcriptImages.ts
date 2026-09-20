// 2026-09-20-3af8: where an image sits in a transcript that has no place for it.
//
// Nothing ties an image to a turn: the upload is a post of its own, racing the message it was
// attached to, and the durable transcript holds text. What both DO carry is a moment, so an
// image is shown after the last turn said before it — which is where the operator put it — and
// an image attached before anything was said leads the conversation.

import type { DialogEntry } from "@/hooks/useSpecDialog";
import type { SpecDialogImage } from "@/types/spec-dialog";

export function withImages(entries: DialogEntry[], images: SpecDialogImage[]): DialogEntry[] {
  if (images.length === 0) return entries;
  const placed: DialogEntry[] = [];
  const pending = [...images].sort((a, b) => Date.parse(a.at) - Date.parse(b.at));
  for (const entry of entries) {
    while (pending.length > 0 && Date.parse(pending[0].at) < Date.parse(entry.at)) {
      placed.push(asEntry(pending.shift()!));
    }
    placed.push(entry);
  }
  return [...placed, ...pending.map(asEntry)];
}

function asEntry(image: SpecDialogImage): DialogEntry {
  return { key: `image-${image.id}`, kind: "image", text: "", at: image.at, image };
}
