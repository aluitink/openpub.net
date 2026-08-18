# Fediblog — UI & UX Plan

**Last Updated:** Aug 18, 2026
**Status:** Backend federation, API, and scalability infrastructure is **feature-complete** (Phases 1–45, 1021/1021 tests passing). This plan is **UI-first**: raise the WebUI to a polished, fast, responsive, accessible microblog. New backend work is deferred until a UI feature specifically requires it.

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
1. ✅ Client-side "load more" + IntersectionObserver infinite scroll on Timeline **and** Search (`?page=` cursor, `data-next`, de-dupe by `data-activity-id`, skeleton loader, cursor hidden on the last page, `&amp;`-decoded cursor URLs).
2. ✅ Defer non-critical note cards; audit `fetchpriority`/`loading="lazy"` on images — every `<img>` now has an explicit loading/fetchpriority strategy + dimensions; note images reserve a stable box (aspect-ratio + min-height) so lazy loads cause no layout shift.
3. ✅ `prefers-reduced-motion: reduce` block in `site.css` zeroes animation/transition durations and scroll-behavior.
 4. ✅ Measured LCP/TTI on home timeline via delegated Playwright subagent. Empty-timeline baseline: LCP 32 ms, FCP 32 ms, load 20 ms, total JS payload ≈ 29 KB (app.js 26 KB + menu.js 2 KB + theme.js 0.4 KB), CSS 300 B. All assets served from cache; no images/fonts/third-party resources. Populated-timeline re-measure deferred until Phase 47 (responsive pass adds real content).
 5. ✅ Consolidate all JS into a single `window.FB` module loader (`app.js`) + feature modules (`menu.js`, `compose.js`, `poll.js`, `search.js`, `suggestions.js`); `theme.js` bootstraps dark mode pre-SSR; SignalR CDN script deduped in the layout.

### Phase 47: Responsive & Mobile-First
1. ✅ Audited pages at 320 / 768 / 1024 / 1440px via delegated Playwright; fixed horizontal overflow (none at any width) and raised touch-targets to ≥44px (`.btn`, `.btn-action`, `.btn-more`, `.btn-unfollow`, `.nav-hamburger`, `.theme-toggle`, `.nav-group-toggle`, `.search-tab`, `.filter-tab`, `.cw-toggle-btn`, `.note-more-item`). Also fixed a pre-existing malformed `site.css` where the `@media (max-width: 768px)` block was missing its opening brace, leaving `.form-actions`/`.hero-actions`/`.error-*`/`.profile-banner`/`.profile-avatar` un-scoped.
2. ✅ Sticky compose FAB (`.compose-fab`, shown ≤768px, hides the in-header compose button) + mobile bottom nav (`.mobile-bottom-nav`, Home/Search/Inbox/Profile) shown ≤768px, hidden ≤480px where the drawer suffices; `body` gains bottom padding so content clears the fixed nav.
3. ✅ Mobile drawer + scrim now keyboard- and screen-reader-operable: `Esc` closes, `aria-hidden` toggles, focus restored to trigger on close, Tab focus-trap, click-outside close. `menu.js` moved to `_Layout` (deduped from 4 views) and `app.js` now loads **before** `menu.js`/view scripts so `window.FB` exists (fixes pre-existing `ReferenceError: FB is not defined` on every module script).
4. ✅ Media queries for note cards, poll bars, admin tables (`.admin-table` horizontal-scroll wrapper as last resort), attachment grids, font-size floors.

### Phase 48: Interaction & Real-Time UX
1. ✅ Live timeline refresh via SignalR (new notes prepend without reload) + SSE fallback.
2. ✅ Notifications: real-time badge + unread counts (server-seeded via `/notifications/badge`), mark-as-read (persisted unread cursor in `UserPreferences`), relative timestamps, deep links to source note (`/timeline?note=<id>` → scroll + flash). Likes/boosts/replies now address the target author in their `to`, so they land in the recipient's inbox; `NewNotification` SignalR event emitted on like/boost/reply/follow.
3. ✅ Optimistic UI everywhere safe, with rollback on failure; replace form-submit reloads with `fetch` + DOM patch. Like/boost now reconcile with the server-rendered card fragment (`/timeline/card/<id>`) after a mutation (single delegated handler, safe for live-inserted cards). Follow/unfollow (profile) and community join/leave are optimistic toggles with rollback + server reconciliation (`/Profile/State`, `/communities/show`). Fixed `ExtractNote` to rehydrate the `Object` property from stored `JsonData` (JsonElement), so boosted/rehydrated notes render with correct interaction counts; fixed `GetFollowerCountAsync` to exclude `Undo` activities so the follower count drops after an unfollow.
4. ✅ Command palette / global search — Ctrl+K / ⌘K overlay (or the header "⌘ Ctrl K" trigger) fuzzy-matches notes, people, hashtags, and communities in one box with keyboard nav (↑/↓ + Enter + Esc). Results come from a new compact `GET /search/json?q=` endpoint and are fuzzy-scored client-side (ordered-subsequence match with position/recency + substring bonuses); groups render capped at 6 each with match highlighting. Selecting navigates to the deep link (note → `/timeline?note=<id>`, user → `/Profile?username=<u>`, community → `/communities/show?communityId=<id>`, hashtag → `/search?tab=hashtags`); with no match, Enter opens the full search page. New `wwwroot/js/palette.js` (FB module, loaded globally for authenticated users) + overlay markup in `_Layout.cshtml` + `.palette-*` styles.

### Phase 49: Design System & Visual Consistency
1. ⬜ Extract component kit from `site.css` (`.note-card`, `.btn*`, `.admin-card`, `.stat-card`, `.avatar-*`, `.empty-state`, `.page-header`); remove ad-hoc inline `<style>`/`style=""`.
2. ⬜ Standardize spacing/typography tokens; no one-off metrics.
3. ⬜ Single inline-SVG icon set (dependency-free), replacing mixed emoji/glyph icons.
4. ⬜ Empty states, skeletons, loading affordances on every data-bearing page.
5. ✅ Error + 404/403/500 pages on-brand. `UseStatusCodePagesWithReExecute` now re-runs a new `GET /Home/StatusError?id=<code>` action that reads the original status and renders one shared, status-aware view (`Views/Home/StatusError.cshtml`) — previously *every* non-2xx code (401/400/403/404) was funneled into a hard-coded 404 page and the `?id` param was ignored. Distinct on-brand pages for 404 / 403 (Access Denied) / 410 / 429 / 500 / 502 / 503 / 504 + a generic fallback, each with a dependency-free inline-SVG icon (lock / magnifier / clock / warning-triangle), tone-colored (danger/info/warning) and theme-aware. A direct `GET /Home/Forbidden` route serves the 403 page. `.error-*` CSS modernized onto the design tokens (`--color-*`, `--radius-*`, `--space-*`, `--font-*`) with a circular icon badge + mobile sizing; `Error.cshtml` (500) and `NotFound.cshtml` reuse the same markup. 6 new tests (status re-execution 404, StatusError default/403/503, Forbidden route, unknown-code fallback) — 1021/1021 passing.

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

### Phase 52: Real-World Federation Testing
Exercise the full stack against live remote instances and real users (integration host `https://openpub.luit.ink/`, primary target **@RayvenMX@mastodon.world**).
0. ⬜ **Webfinger validation (remote→local)**: verify remote instances can resolve our users via Webfinger — `GET https://openpub.luit.ink/.well-known/webfinger?resource=acct:<username>@openpub.luit.ink` returns `200` with the correct `Link rel="activitypub"` pointing at our user's ActivityPub profile document, which in turn returns the correct `Person` (id, preferredUsername, name, summary, avatar, followersCollection, outbox). Cross-check from a remote instance (e.g. search our handle on mastodon.world / a second instance) that the profile appears and renders like a standard Mastodon profile (avatar, display name, bio, follower/following counts, post list) — this is the current blocker: our user is **not found** from remote instances.
1. ⬜ **Locate real users**: follow **@RayvenMX@mastodon.world** from the Fediblog UI; verify the follow request/acceptance round-trip, the follower count updates on their profile, and the confirmation notification lands in our inbox.
2. ⬜ **Browse real remote profiles**: open @RayvenMX's profile and their posts via our UI; verify remote avatars, banners, note rendering, interaction counts, and pagination of their timeline.
3. ⬜ **Interact with a real user**: reply to one of their notes and like/boost it; verify the Activity is delivered (visible on their side / in their mentions), our optimistic UI reconciles with server truth, and notifications/badges update when they respond.
4. ⬜ **Inbound federation**: when the remote user interacts with us (likes, boosts, replies, follows back), verify we receive, store, deduplicate, and surface it (notifications, timeline, profile counts).
5. ⬜ **Federation health pass**: confirm outbound/inbound delivery latency, no duplicate or lost activities, graceful handling of remote errors/timeouts, and that remote content renders consistently across light/dark themes and at 320px.
6. ⬜ **Cleanup**: unfollow/unlike/unboost test interactions so we leave no lasting footprint on the remote user's account.

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
