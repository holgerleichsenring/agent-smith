"use client";

import type { ButtonHTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/utils";

// p0219: shared button primitive. The DESIGN.md button-* spec is marketing-
// scaled (18px label, large padding); the dashboard is dense, so this realises
// the same language — brand-green primary, rounded-md, ghost outline — at the
// operator scale. One definition replaces ad-hoc per-component button styling.

type ButtonVariant = "primary" | "ghost" | "subtle";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  children: ReactNode;
}

const VARIANTS: Record<ButtonVariant, string> = {
  primary:
    "border border-transparent bg-[var(--color-primary)] text-[var(--color-on-primary)] hover:bg-[var(--color-primary-pressed)]",
  ghost: "border border-stone-300 bg-[var(--color-canvas)] text-stone-700 hover:bg-stone-100",
  subtle: "border border-transparent text-stone-600 hover:bg-stone-100",
};

export function Button({ variant = "ghost", className, children, type, ...rest }: ButtonProps) {
  return (
    <button
      type={type ?? "button"}
      className={cn(
        "inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 dsh-body font-medium transition disabled:opacity-50",
        VARIANTS[variant],
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  );
}
