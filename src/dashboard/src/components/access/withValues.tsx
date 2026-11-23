import { Fragment, type ReactNode } from "react";

// 2026-11-23-5669e: a dictionary text whose placeholders are drawn as elements — a bold count, a mono value — rather
// than as plain text. Every {name} found in `values` becomes that node; any other text stays as written.

/** `text` with each {name} replaced by values[name], as a list of nodes. */
export function withValues(text: string, values: Record<string, ReactNode>): ReactNode[] {
  return text.split(/(\{\w+\})/).map((part, at) => {
    const name = /^\{(\w+)\}$/.exec(part)?.[1];
    return <Fragment key={at}>{name !== undefined && name in values ? values[name] : part}</Fragment>;
  });
}
