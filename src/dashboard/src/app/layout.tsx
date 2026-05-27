import type { Metadata } from "next";
import { Inter } from "next/font/google";
import Link from "next/link";
import "./globals.css";

// p0174: Inter is the DESIGN.md primary typography — load via next/font
// so it's self-hosted, font-display:swap by default, and bound to a CSS
// variable the rest of the app consumes via Tailwind's font-sans utility.
const inter = Inter({
  subsets: ["latin"],
  variable: "--font-sans",
  display: "swap",
});

export const metadata: Metadata = {
  title: "agent-smith",
  description: "AI orchestration framework — self-hosted",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={inter.variable}>
      <body className="bg-[var(--color-canvas)] text-[var(--color-ink)] font-sans">
        <nav className="border-b border-stone-200 bg-[var(--color-canvas)]/95 px-6 py-3 text-sm backdrop-blur">
          <div className="mx-auto flex max-w-6xl items-center gap-6">
            <Link
              href="/"
              className="flex items-center gap-2 font-medium text-[var(--color-ink)] hover:text-[var(--color-ink-soft)]"
            >
              <span
                aria-hidden
                className="inline-block h-2 w-2 rounded-full bg-[var(--color-primary)]"
              />
              agent-smith
            </Link>
            <Link href="/" className="text-[var(--color-ink-soft)] hover:text-[var(--color-ink)]">
              Runs
            </Link>
            <Link href="/system" className="text-[var(--color-ink-soft)] hover:text-[var(--color-ink)]">
              System
            </Link>
          </div>
        </nav>
        {children}
      </body>
    </html>
  );
}
