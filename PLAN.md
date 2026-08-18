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
- [ ] Item 2 (in progress): tokenize spacing. **Iteration 3 done (single-value):** converted all **102 single-value `padding`/`margin`/`gap`** literals to the `--space-*` 0.25rem grid — exact-grid restatements (`0.25/0.5/0.75rem`, `1/1.5/2rem`, `3rem`, `4/8/12px`) are zero-change; the off-rhythm values (`0.6rem`→space-2, `0.4rem`→space-2, `0.35rem`→space-1, `0.15rem`→space-1, `1.25rem`→space-5, `8px`→space-2, `12px`→space-3) rounded to nearest. Only the deliberate `.sr-only` `margin: -1px` overlap stays raw. Line-height, border-radius, and font-size tokenization already done. 1 new test (`ComponentKitTests`: single-value padding/margin/gap use `--space-*` tokens, CSS comments stripped so doc-snippet examples don't false-positive) — **1137/1137 passing**. **Remaining (next iteration):** the ~125 **multi-value** shorthand literals (`padding: 1rem 2rem`, `margin: 1rem 0 0.5rem`, `gap: 0.5rem 1rem`, …) + the `top/right/bottom/left` offset literals (incl. negative overlap hacks & precise modal/hero positioning) → the grid, case-by-case.
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
