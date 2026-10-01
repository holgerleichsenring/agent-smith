// 2026-10-01-aeb6b: a design reply may carry DOCUMENTS — a text the operator asked for to take
// elsewhere, fenced with four backticks so it can hold ``` code of its own. The reply is split
// before markdown sees it: a document is shown as a card with its raw text one click from the
// clipboard, and the prose around it renders as before. A fence the reply cap cut off before it
// closed is a document to the end, so the part that did arrive is still copyable.

export type ReplyPart = { kind: "prose"; text: string } | { kind: "document"; text: string };

const OPEN = /^````document[^\n]*\n/m;
const CLOSE = /^````[ \t]*$/m;

export function documentBlocks(reply: string): ReplyPart[] {
  const parts: ReplyPart[] = [];
  let rest = reply;
  for (let open = OPEN.exec(rest); open; open = OPEN.exec(rest)) {
    pushProse(parts, rest.slice(0, open.index));
    const body = rest.slice(open.index + open[0].length);
    const close = CLOSE.exec(body);
    parts.push({ kind: "document", text: (close ? body.slice(0, close.index) : body).replace(/\n$/, "") });
    rest = close ? body.slice(close.index + close[0].length) : "";
  }
  pushProse(parts, rest);
  return parts;
}

/** What a document is called: its first heading, or nothing when it has none. */
export function documentTitle(text: string): string | null {
  const heading = /^#{1,6}\s+(.+)$/m.exec(text);
  return heading ? heading[1].trim() : null;
}

function pushProse(parts: ReplyPart[], text: string) {
  if (text.trim().length > 0) parts.push({ kind: "prose", text: text.trim() });
}
