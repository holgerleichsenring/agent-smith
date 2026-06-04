"use client";

import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";

// p0219: shared card primitive over the DESIGN.md card surfaces — the cream
// content card and the brand terminal panel (dark mono surface for CLI / code
// / log output). surface picks which.

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  surface?: "content" | "terminal";
  children: ReactNode;
}

export function Card({ surface = "content", className, children, ...rest }: CardProps) {
  return (
    <div
      className={cn(surface === "terminal" ? "card-terminal-panel" : "card-content", className)}
      {...rest}
    >
      {children}
    </div>
  );
}
