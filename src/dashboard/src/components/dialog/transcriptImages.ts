// 2026-09-20-3af8: where an image sits in a transcript that has no place for it.
//
// Nothing ties an image to a turn: the upload is a post of its own, racing the message it was
// attached to, and the durable transcript holds text. What both DO carry is a moment, so an
// image is shown after the last turn said before it — which is where the operator put it — and
// an image attached before anything was said leads the conversation.
// 2026-10-01-283db: an uploaded website is placed by the same rule, as one entry per set.
// 2026-10-13-8762r: and a mock version the design partner wrote, as one entry per version.

import type { DialogEntry, TranscriptMock } from "@/hooks/useSpecDialog";
import type { SpecDialogImage, SpecDialogReferenceSet } from "@/types/spec-dialog";

export function withImages(
  entries: DialogEntry[],
  images: SpecDialogImage[],
  references: SpecDialogReferenceSet[] = [],
  mocks: TranscriptMock[] = [],
): DialogEntry[] {
  const attached = [...images.map(imageEntry), ...references.map(referenceEntry), ...mocks.map(mockEntry)];
  if (attached.length === 0) return entries;
  const placed: DialogEntry[] = [];
  const pending = attached.sort((a, b) => Date.parse(a.at) - Date.parse(b.at));
  for (const entry of entries) {
    while (pending.length > 0 && Date.parse(pending[0].at) < Date.parse(entry.at)) {
      placed.push(pending.shift()!);
    }
    placed.push(entry);
  }
  return [...placed, ...pending];
}

function imageEntry(image: SpecDialogImage): DialogEntry {
  return { key: `image-${image.id}`, kind: "image", text: "", at: image.at, image };
}

function referenceEntry(reference: SpecDialogReferenceSet): DialogEntry {
  return { key: `reference-${reference.setId}`, kind: "reference", text: "", at: reference.at, reference };
}

/** 2026-10-13-8762r: a version's entry key — what an unarmed read checks before it adds one. */
export function mockKey(mock: TranscriptMock): string {
  return `mock-${mock.specId}-v${mock.version}`;
}

function mockEntry(mock: TranscriptMock): DialogEntry {
  return { key: mockKey(mock), kind: "mock", text: "", at: mock.at, mock };
}

/** 2026-10-13-8762r: the mocks a read carries that the entries do not hold yet, placed by the same rule;
 *  a held one whose screenshot has since been stored is refreshed in place. Every read applies it, armed or
 *  not, so a mock written in a turn shows without a reload. */
export function withMocks(entries: DialogEntry[], mocks: TranscriptMock[]): DialogEntry[] {
  const fresh = new Map(mocks.map((mock) => [mockKey(mock), mock]));
  const held = new Set(entries.map((entry) => entry.key));
  const refreshed = entries.map((entry) => {
    const now = fresh.get(entry.key);
    return now && entry.mock && entry.mock.hasShot !== now.hasShot ? { ...entry, mock: now } : entry;
  });
  const unheld = mocks.filter((mock) => !held.has(mockKey(mock)));
  return unheld.length === 0 && refreshed.every((entry, i) => entry === entries[i])
    ? entries
    : withImages(refreshed, [], [], unheld);
}
