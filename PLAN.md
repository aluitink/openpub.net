# Fediblog — UI & UX Plan

**Last Updated:** Aug 17, 2026
**Status:** Backend federation, API, and scalability infrastructure is **feature-complete** (Phases 1–45, 966/966 tests passing). This plan is **UI-first**: raise the WebUI to a polished, fast, responsive, accessible microblog. New backend work is deferred until a UI feature specifically requires it.

**WebUI:** `src/ActivityPub.WebUI/` — Razor MVC, SignalR, vanilla JS (`wwwroot/js/`: `compose.js`, `menu.js`, `toast.js`), single `site.css` with `:root` design tokens + `[data-theme=dark]` overrides.

---

## UI Testing (brief)

- Browser/UI tests run in **delegated Playwright subagents** (not inline), after each meaningful change batch, before marking a feature complete.
- **Launch:** `docker compose -f src/ActivityPub.WebUI/docker-compose.yml up -d --build`; wait for HTTP 200 at `http://localhost:8080`.
- Exercise changed flows (auth, compose, timeline, interactions, profiles, admin); verify elements, text, behavior; report pass/fail with screenshots + console errors.
- **Localhost:** no real federation — mock remote rows directly in the DB (SQLite in the `fediblog-data` volume) or POST crafted ActivityPub payloads to the local inbox.
- **Integration host:** `https://openpub.luit.ink/` (real federation; test target **@RayvenMX@mastodon.world**).
- **Cleanup:** `docker compose -f src/ActivityPub.WebUI/docker-compose.yml down` (add `-v` to discard DB fixtures).

---

## Phases

### Phase 46: UI Performance & Perceived Speed
Make the timeline and interactions feel instant.
1. ✅ Timeline: fetch-based "load more" + IntersectionObserver infinite scroll (`?page=` cursor, `data-next`, de-dupe by `data-activity-id`, skeleton loader, cursor hidden on the last page). Remaining: extend the same loader to Search.
2. ⬜ Defer non-critical note cards; audit `fetchpriority`/`loading="lazy"` on images.
3. ✅ `prefers-reduced-motion: reduce` block in `site.css` zeroes animation/transition durations and scroll-behavior.
4. ⬜ Measure LCP/TTI on home timeline via delegated subagent (before/after screenshots + JS perf snapshot).
5. ⬜ Consolidate the 3 JS files + inline scripts into a small module loader (no framework); dedupe SignalR bootstrap.

### Phase 47: Responsive & Mobile-First
1. ⬜ Audit every page at 320 / 768 / 1024 / 1440px via delegated Playwright; fix overflow, touch-target (<44px), font-size issues.
2. ⬜ Sticky compose FAB on mobile; bottom nav or collapsed drawer on small screens.
3. ⬜ Mobile drawer + scrim keyboard- and screen-reader-operable (focus trap, `Esc` closes).
4. ⬜ Media queries for note cards, poll bars, admin tables (horizontal scroll only as last resort).

### Phase 48: Interaction & Real-Time UX
1. ⬜ Live timeline refresh via SignalR (new notes prepend without reload) + SSE fallback.
2. ⬜ Notifications: real-time badge + unread counts, mark-as-read, relative timestamps, deep links to source note.
3. ⬜ Optimistic UI everywhere safe, with rollback on failure; replace form-submit reloads with `fetch` + DOM patch.
4. ⬜ Command palette / global search — fuzzy match across notes, users, hashtags, communities.

### Phase 49: Design System & Visual Consistency
1. ⬜ Extract component kit from `site.css` (`.note-card`, `.btn*`, `.admin-card`, `.stat-card`, `.avatar-*`, `.empty-state`, `.page-header`); remove ad-hoc inline `<style>`/`style=""`.
2. ⬜ Standardize spacing/typography tokens; no one-off metrics.
3. ⬜ Single inline-SVG icon set (dependency-free), replacing mixed emoji/glyph icons.
4. ⬜ Empty states, skeletons, loading affordances on every data-bearing page.
5. ⬜ Error + 404/403/500 pages on-brand.

### Phase 50: Accessibility (WCAG AA)
1. ⬜ Contrast re-audit across light **and** dark themes (all views).
2. ⬜ Focus-visible + logical tab order on all interactive controls (incl. note-more dropdown, poll options).
3. ⬜ Screen-reader pass: `aria-*` on dropdowns/menus/modals, live regions for toasts + timeline inserts, `alt`/`aria-label` on every icon button.
4. ⬜ Keyboard-only walkthrough of entire app via delegated subagent; fix any trap.

### Phase 51: Rich Media in the UI (leverage existing backend)
1. ⬜ Image lightbox with keyboard nav + prev/next; proper `alt` text.
2. ⬜ Multi-image attachments in a grid.
3. ⬜ Video/audio/document rendering with native players + thumbnails (surface existing backend support first).
4. ⬜ Link previews / OEmbed for outbound URLs (client-side, v1).
5. ⬜ Content-warnings: blur + reveal, per-note and global; respected in lightbox.

### Deferred (only if/when a UI feature requires it)
- OEmbed server-side, upload virus scan, size limits.
- Load testing with 100+ concurrent users (backend).

---

## Acceptance Criteria

- No page uses one-off styling inconsistent with the rest of the app.
- Dark mode toggles without flicker and persists; both themes pass WCAG AA.
- Every page is usable by keyboard alone and at 320px width.
- Timeline feels instant: new notes appear without full reload; pagination is client-driven.
- All changes verified by a delegated Playwright subagent with before/after screenshots.
