import type { SpecDialogView } from "@/types/spec-dialog";

// A reply push is a hint; the stored assistant turn is the fact. A read issued after the post that
// shows the turn finished and the transcript grown past where it stood, ending in the agent's turn,
// is the reply recorded — whether or not its push ever arrived. Positions are compared, never
// moments, so the server's clock and this browser's are never set against each other.

/** Where the conversation stood when the page posted: the read count and the transcript length. */
export interface AwaitedReply {
  read: number;
  turns: number;
}

/** Whether the read numbered `issued` shows the reply to the post `awaited` stands for, stored. */
export function replyRecorded(view: SpecDialogView, awaited: AwaitedReply | null, issued: number): boolean {
  if (awaited === null || issued <= awaited.read || view.turn?.computing) return false;
  const transcript = view.session?.transcript ?? [];
  return transcript.length > awaited.turns && transcript[transcript.length - 1].role === "assistant";
}
