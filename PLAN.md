# ActivityPub.NET / Fediblog — Project Plan

**Last Updated:** Aug 17, 2026
**Status:** Phases 1–45 complete. **966/966 tests passing.** Backend federation, API, and scalability infrastructure is now **feature-complete** — the focus shifts to the **WebUI**.

> **Direction:** We have enough backend infrastructure for now (federation, HTTP signatures, delivery retry + DLQ, peer health, Redis cache + backplane, PostgreSQL migration, REST API + OAuth, leak-free memory). The remainder of this plan is **UI-first**: raise the WebUI to a polished, fast, responsive, accessible microblog. New backend work is deferred until a UI feature specifically requires it.

---

## Build State

- **Build:** 0 errors
- **Tests:** 966 passing, 0 failures
- **Framework:** .NET 10.0
- **Branch:** qwen3.6-27b-eval
- **WebUI:** `src/ActivityPub.WebUI/` — Razor MVC, SQLite, username/password auth, SignalR, vanilla JS (`wwwroot/js/`: `compose.js`, `menu.js`, `toast.js`), single `site.css` (~3.6k lines) with `:root` design tokens + `[data-theme=dark]` overrides.

---

## UI Testing Guidelines (read before touching the WebUI)

- **Browser/UI tests using Playwright** run in **delegated subagents**, not inline, to isolate browser state.
- After each meaningful WebUI change batch, run QA via a delegated Playwright subagent **before** marking a feature complete.

**Workflow (delegate to a subagent):**
1. **Launch:** `docker compose -f src/ActivityPub.WebUI/docker-compose.yml up -d --build`; wait for HTTP 200 on the base URL.
2. **Test with Playwright:** navigate to `http://localhost:8080` (or `https://openpub.luit.ink/` for federation), exercise the changed flows (auth, compose, timeline, interactions, profiles, admin, …) using navigation/snapshot/click/fill/screenshot tools. Verify elements, text, behavior; evaluate screenshots.
3. **Report:** pass/fail summary with screenshots for failures + any console errors.

**Localhost constraints & mocking:**
- Against `localhost` there is **no real routability/federation** — don't expect cross-server delivery. (Pointed at `https://openpub.luit.ink/`, real federation incl. following real users works.)
- Where a test needs remote data (remote actors, notes, follows, federation replies), **mock rows directly in the DB** (SQLite `/data/ap.db` + `/data/app.db` in the `fediblog-data` volume) instead of real federation. For inbox-driven flows, POST crafted ActivityPub payloads to the local inbox endpoint.

**Cleanup:** `docker compose -f src/ActivityPub.WebUI/docker-compose.yml down` (add `-v` only to discard DB fixtures).

**Public deployment / integration host:** `https://openpub.luit.ink/` (Docker compose + reverse proxy terminates TLS). Set `ActivityPub:Domain=https://openpub.luit.ink`. Real-world federation test target: **@RayvenMX@mastodon.world** (follow/unfollow, reply, like, inbox delivery).

---

## Completed Work (summary)

### Core library — Phases 1–22 (foundation, docs, tooling, tests, benchmarks, compliance)
Foundation & directory structure; README/guides; build + CI/CD + quality; cleanup/consolidation; source migration; structure validation; full test suite (502 at the time); migration verification; API/migration docs; production readiness; JWT identity; code quality (nullable, packages); performance (caching, batching); security (headers, validation); deployment (Docker, K8s); .gitignore; GitHub Actions; admin dashboard (Razor Pages); CLI tool (System.CommandLine); integration tests; BenchmarkDotNet; Observatory compliance.

### Fediblog WebUI + federation + API + scale — Phases 25–45

| Phase | Title (condensed) |
|-------|-------------------|
| 25 | WebUI foundation & auth — registration, login, actor seeding, layout |
| 26 | Compose & timeline — note creation, home/public timelines, delete |
| 27 | Follows & federation — follow/unfollow, remote actor discovery |
| 28 | Interactions — like, reply, boost with threading + counts |
| 29 | Profiles & actor endpoints — profile pages, outbox, followers/following/liked |
| 30 | Polish & production — responsive CSS, error pages, rate limiting, hashtags, search, Docker |
| 31 | Performance — DB indexes, response caching, query optimization |
| 32 | Admin & moderation — dashboard, roles, MRF, audit log, reports, federation health |
| 33 | Extended federation — inbox processor, outbox pagination, articles, image uploads, polls, editable notes, block |
| 34 | Real-time & notifications — SignalR hub, SSE, push, desktop alerts |
| 35 | Content discovery & communities — suggestions, trending hashtags, content filtering, communities |
| 36 | Media & rich content — **deferred** (see Open Work; backend for most now exists) |
| 37 | API & DX — Mastodon-compatible REST API, app registration, OAuth2 PKCE, rate limits, Swagger, webhooks |
| 38 | Federation hardening — HTTP signature verification, delivery retry + backoff, peer health auto-block, real S2S testing, inbox DLQ |
| 39 | Scalability — Redis cache, WebSocket backplane + distributed rate limiting, PostgreSQL migration, leak detection (T5 load testing deferred) |
| 40 | Navigation & menu — grouped dropdowns, mobile drawer, active-route highlight |
| 41 | Page completeness & navigation audit — RouteAuditTests, empty states, back-links, role-gating |
| 42 | Core UX — char counter, previews, optimistic like/boost, toasts, skeletons, inline reply |
| 43 | Interface buildout & polish — design tokens, avatars, profile stats, communities, trends, admin, dark mode, a11y, footer, page-header pattern |
| 44 | Look & feel review — P0/P1/P2 screenshot-audit fixes (fresh-context QA re-sweep outstanding) |
| 45 | Consolidate all code under `src/` — Tests, Benchmarks, samples moved; solution/scripts/CI/docs updated |

**Core library surface (unchanged, stable):** Models (Actor, Note, Create, Follow, Like, Announce, Article, Page, Video, Image, collections, Activity + discovery types); `IActivityPubRepository` (actor/activity CRUD, outbox/followers/following/liked, dedup, shared-inbox + webhook queues); services (ActivityPubService, InboxProcessor, OutboundActivity/Signing, FederationDiscovery, KeyFetching/Generation, SharedInbox, WebhookDelivery, WebFingerCache, ActivityValidation, MRF, Cache, EventDispatcher, FederationHealth); middleware (RateLimiting, SecurityHeaders, HttpSignature, SigningVerification); EFCore repository (InMemory/SQLite/PostgreSQL); `AddActivityPub(Action<ActivityPubOptions>?)`; discovery endpoints (WebFinger, NodeInfo 2.1, HostMeta, Health).

---

## Open Work — UI-Focused

The WebUI is functional end-to-end but reads as a competent prototype rather than a polished product. Priorities, in order:

### Phase 46: UI Performance & Perceived Speed
Make the timeline and interactions feel instant.
1. ⬜ Client-side pagination / "load more" + infinite scroll on Timeline & Search (server already paginates; add a `?after=` cursor + JS loader).
2. ⬜ Defer rendering of non-critical note cards; audit `fetchpriority`/`loading="lazy"` on images.
3. ⬜ Wire `prefers-reduced-motion` and disable animation when set.
4. ⬜ Measure with a delegated subagent: LCP/TTI on the home timeline, before/after screenshots + a small JS perf snapshot.
5. ⬜ Consolidate the 3 JS files + inline layout scripts into a small module loader (no framework), dedupe the SignalR bootstrap.

### Phase 47: Responsive & Mobile-First Pass
1. ⬜ Audit every page at 320 / 768 / 1024 / 1440px via delegated Playwright (screenshots per breakpoint); fix overflow, touch-target (<44px), and font-size issues.
2. ⬜ Sticky compose FAB on mobile; bottom nav or collapsed drawer on small screens.
3. ⬜ Ensure the mobile drawer + scrim is keyboard- and screen-reader-operable (focus trap, `Esc` closes).
4. ⬜ Media queries for note cards, poll bars, and admin tables (horizontal scroll only as a last resort).

### Phase 48: Interaction & Real-Time UX
1. ⬜ Live timeline refresh via the existing SignalR hub (new notes prepend without full reload) + SSE fallback.
2. ⬜ Notifications inbox: real-time badge + unread counts, mark-as-read, relative timestamps, deep links to the source note.
3. ⬜ Optimistic UI everywhere it is safe (like/boost/follow already partial) with rollback on failure; replace form-submit reloads with `fetch` + DOM patch where the round-trip is trivial.
4. ⬜ Command palette / global search (`/` already focuses search) — fuzzy match across notes, users, hashtags, communities.

### Phase 49: Design System & Visual Consistency
1. ⬜ Extract a small component kit from `site.css` (`.note-card`, `.btn*`, `.admin-card`, `.stat-card`, `.avatar-*`, `.empty-state`, `.page-header`) into clearly sectioned blocks; remove the remaining ad-hoc inline `<style>` and `style=""` attributes.
2. ⬜ Standardize spacing/typography tokens; verify no page uses one-off metrics (extend the Phase 43 token set).
3. ⬜ Icon consistency — replace mixed emoji/glyph action icons with a single inline-SVG set (keeps it dependency-free).
4. ⬜ Empty states, skeletons, and loading affordances on every data-bearing page (extend Phase 42 work to Communities, Trends, Search, Notifications, Admin).
5. ⬜ Error + 404/403/500 pages on-brand (extend Phase 44).

### Phase 50: Accessibility & Polish (WCAG AA)
1. ⬜ Full contrast re-audit across light **and** dark themes (extend the 6 `AccessibilityTests` to a sweep of all views).
2. ⬜ Focus-visible + logical tab order on all interactive controls, including the note-more dropdown and poll options.
3. ⬜ Screen-reader pass: `aria-*` on dropdowns/menus/modals, live regions for toasts + timeline inserts, `alt`/`aria-label` on every icon button.
4. ⬜ Keyboard-only walkthrough of the entire app (login → compose → like/reply/boost → follow → admin) via a delegated subagent; fix any trap.

### Phase 51: Rich Media in the UI (leverage existing backend)
1. ⬜ Image lightbox with keyboard nav + prev/next; proper `alt` text.
2. ⬜ Multi-image attachments in a grid (backend already stores multiple).
3. ⬜ Video/audio/document rendering with native players + thumbnails (Phase 36 was deferred; surface what the backend already supports before adding new backend).
4. ⬜ Link previews / OEmbed for outbound URLs (client-side, no backend change needed for v1).
5. ⬜ Content-warnings: blur + reveal, per-note and global; respect in lightbox.

### Deferred / lower priority (UI or backend)
- Phase 36 remainder: OEmbed server-side, upload virus scan, size limits (only if/when UI needs them).
- Phase 39 T5: load testing with 100+ concurrent users (backend).
- Fresh-context Playwright QA re-sweep of the Phase 44 fixes (carried over).

**Acceptance criteria for the UI push:**
- No page uses one-off styling inconsistent with the rest of the app.
- Dark mode toggles without flicker and persists; both themes pass WCAG AA.
- Every page is usable by keyboard alone and at 320px width.
- Timeline feels instant: new notes appear without a full reload; pagination is client-driven.
- All changes verified by a delegated Playwright subagent with before/after screenshots.
