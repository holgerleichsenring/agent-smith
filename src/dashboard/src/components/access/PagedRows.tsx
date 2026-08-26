"use client";

import { useState, type ReactNode } from "react";

// 2026-08-26-7a51: the toolbar-and-tally shared by the People and Groups panes — a search
// box, a few quick filters, twelve rows, "Showing 12 of 143" and a show-more button.
//
// One implementation, because both panes page at directory scale and two copies of paging
// is two places for the tally to go wrong.

export const PAGE = 12;
const MORE = 25;

export interface PagedFilter<T> {
  key: string;
  label: string;
  matches: (row: T) => boolean;
}

export function PagedRows<T>({
  rows,
  testId,
  searchLabel,
  searchPlaceholder,
  filters,
  matchesQuery,
  header,
  renderRow,
  emptyText,
  toolbarExtra,
}: {
  rows: T[];
  testId: string;
  searchLabel: string;
  searchPlaceholder: string;
  filters: PagedFilter<T>[];
  matchesQuery: (row: T, query: string) => boolean;
  header: ReactNode;
  renderRow: (row: T) => ReactNode;
  emptyText: string;
  toolbarExtra?: ReactNode;
}) {
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState(filters[0]?.key ?? "");
  const [shown, setShown] = useState(PAGE);

  const active = filters.find((f) => f.key === filter);
  const matching = rows.filter(
    (row) =>
      (query.trim() === "" || matchesQuery(row, query.trim().toLowerCase()))
      && (active === undefined || active.matches(row)),
  );
  const page = matching.slice(0, shown);

  function narrow(change: () => void) {
    change();
    setShown(PAGE);
  }

  return (
    <>
      <div className="toolbar">
        <input
          className="search"
          type="search"
          aria-label={searchLabel}
          placeholder={searchPlaceholder}
          data-testid={`${testId}-search`}
          value={query}
          onChange={(e) => narrow(() => setQuery(e.target.value))}
        />
        <div className="filters">
          {filters.map((f) => (
            <button
              key={f.key}
              type="button"
              className="filter"
              aria-pressed={f.key === filter}
              data-testid={`${testId}-filter-${f.key}`}
              onClick={() => narrow(() => setFilter(f.key))}
            >
              {f.label}
            </button>
          ))}
        </div>
        {toolbarExtra}
      </div>
      <div className="tablewrap">
        <table>
          <thead>{header}</thead>
          <tbody data-testid={`${testId}-rows`}>
            {page.length === 0 ? (
              <tr>
                <td colSpan={4} className="empty" data-testid={`${testId}-empty`}>
                  {emptyText}
                </td>
              </tr>
            ) : (
              page.map(renderRow)
            )}
          </tbody>
        </table>
      </div>
      <div className="more">
        <span className="tally" data-testid={`${testId}-tally`}>
          {matching.length > 0 ? `Showing ${page.length} of ${matching.length}` : ""}
        </span>
        {page.length < matching.length && (
          <button
            type="button"
            className="more-btn"
            data-testid={`${testId}-more`}
            onClick={() => setShown(shown + MORE)}
          >
            Show {Math.min(MORE, matching.length - page.length)} more
          </button>
        )}
      </div>
    </>
  );
}
