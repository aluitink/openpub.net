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
 - [ ] Item 2 (in progress): tokenize spacing. **Iteration 4 done (multi-value shorthands):** converted all **138 multi-value `padding`/`margin`/`gap`** shorthands (`padding: 0.5rem 0.75rem`, `margin: 1.5rem 0 0.75rem`, `gap: 0.4rem 1.4rem`, …) to the `--space-*` 0.25rem grid — on-grid values restated exactly (zero change), off-rhythm values rounded to nearest. Permitted raw forms that stay as-is: `0`/`auto` (reset/centering), negative lengths (the `.sr-only` `margin:-1px` overlap + negative positioning hacks), and non-scale units (`vh`/`vw`/`em`/`%`); `var()`/`calc()` are already token-derived. Custom properties (`--btn-padding`, …) and `/* … */` comment snippets are left untouched. Done with a comment- + custom-prop-aware transform (verified: braces balanced, doc snippets intact, `.sr-only` preserved). The `ComponentKitTests` spacing test was **extended** from single-value to **every length token** in padding/margin/gap (single + multi-value) — **1137/1137 passing**. **Remaining (next iteration):** the `top/right/bottom/left` offset literals (incl. negative overlap hacks & precise modal/hero positioning) → the grid, case-by-case.
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
