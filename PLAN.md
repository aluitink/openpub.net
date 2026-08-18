# Fediblog — UI & UX Plan

**Last Updated:** Aug 18, 2026
**Status:** Backend federation, API, and scalability infrastructure is feature-complete (Phases 1–45). UI phases 46–52 mostly done; only the items below remain. New backend work is deferred until a UI feature requires it.

**WebUI:** `src/ActivityPub.WebUI/` — Razor MVC, SignalR, vanilla JS (`wwwroot/js/`), single `site.css` with `:root` design tokens + `[data-theme=dark]` overrides.

## UI Testing (brief)

- Browser/UI tests run in delegated Playwright subagents after each meaningful change batch, before marking a feature complete.
- Launch: `docker compose -f src/ActivityPub.WebUI/docker-compose.yml up -d --build`; wait for HTTP 200 at `http://localhost:8080`.
- Localhost has no real federation — mock remote rows in the DB (SQLite in `fediblog-data`) or POST crafted ActivityPub payloads to the local inbox.
- Integration host: `https://openpub.luit.ink/` (test target **@RayvenMX@mastodon.world**).
- Cleanup: `docker compose -f src/ActivityPub.WebUI/docker-compose.yml down` (add `-v` to discard DB fixtures).

## Remaining Work

### Phase 49: Design System & Visual Consistency
  - [x] Item 2 (done): tokenize spacing. **All iterations complete.** Iter 1–2: line-height + font-size + border-radius tokens. Iter 3: all **102 single-value** `padding`/`margin`/`gap` shorthands → the `--space-*` 0.25rem grid. Iter 4: all **138 multi-value** shorthands → the grid. Iter 5: all **101 longhand** `margin-(top|right|bottom|left)` / `padding-(top|right|bottom|left)` rhythm values → the grid (on-grid restated exactly, off-rhythm rounded; sub-4px pixel nudges like `margin-top:1px` kept raw as alignment, not rhythm). Permitted raw forms across all iterations: `0`/`auto` (reset/centering), negative lengths (`.sr-only` `margin:-1px` overlap + modal/hero negative positioning hacks), non-scale units (`vh`/`vw`/`em`/`%`), CSS functions (`var()`/`calc()`/`env()`, incl. `env(safe-area-inset-*)`), and custom properties (`--*`) + `/* … */` comment snippets. The `ComponentKitTests` spacing guard now scans **every** padding/margin/gap length token (shorthand + longhand, single + multi-value, paren-aware split so function calls stay whole) — **1137/1137 passing**. **Remaining (optional):** the pure `top/right/bottom/left` positioning offsets (centering `50%`/`100%`, sub-pixel nudges, negative modal hacks) are deliberately left raw — they are layout positioning, not rhythm spacing.
- [ ] Item 4: empty states, skeletons, and loading affordances on every data-bearing page.

### Phase 52: Real-World Federation Testing
Exercise the full stack against live remote instances (integration host `https://openpub.luit.ink/`, target **@RayvenMX@mastodon.world**). Webfinger local-side root cause is fixed (`WebFingerFederationTests`); the live cross-instance check below is still pending.
- [ ] Live cross-instance webfinger check: confirm @RayvenMX's profile appears from a remote instance and renders like a standard Mastodon profile (avatar, display name, bio, follower/following counts, post list).
- [ ] Locate real users: follow @RayvenMX from the Fediblog UI; verify follow request/acceptance round-trip, follower count updates, confirmation notification in our inbox.
- [ ] Browse real remote profiles: open their profile and posts via our UI; verify remote avatars, banners, note rendering, interaction counts, and timeline pagination.
- [ ] Interact with a real user: reply to and like/boost one of their notes; verify delivery on their side, optimistic UI reconciles with server truth, notifications/badges update on their response.
- [ ] Inbound federation: when they interact with us (likes, boosts, replies, follow-back), verify we receive, store, dedupe, and surface it (notifications, timeline, profile counts).
- [ ] Federation health pass: outbound/inbound delivery latency, no duplicate/lost activities, graceful handling of remote errors/timeouts, remote content renders consistently in light/dark and at 320px.
- [ ] Cleanup: unfollow/unlike/unboost test interactions so we leave no lasting footprint on the remote user's account.

### Deferred (only if/when a UI feature requires it)
- OEmbed server-side, upload virus scan, size limits.
- Load testing with 100+ concurrent users (backend).

## Acceptance Criteria

- No page uses one-off styling inconsistent with the rest of the app.
- Dark mode toggles without flicker and persists; both themes pass WCAG AA.
- Every page is usable by keyboard alone and at 320px width.
- Timeline feels instant: new notes appear without full reload; pagination is client-driven.
- All changes verified by a delegated Playwright subagent with before/after screenshots.
