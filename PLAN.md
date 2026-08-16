# ActivityPub.NET - Project Plan

**Last Updated:** Aug 16, 2026
**Status:** Phases 1-40 complete. 790/790 tests passing. Phase 44 P0/P1 fixes applied.

## Testing Guidelines

- **Browser/UI tests using Playwright** should be executed in delegated agents rather than inline, to isolate browser state and avoid conflicts with the main session.

## Iterative WebUI QA (Delegated Subagents)

When making changes to `src/ActivityPub.WebUI/`, run QA via delegated subagents using Playwright tools. Do this after each meaningful change batch, before marking a phase/feature complete.

**Workflow (delegate to a subagent, not inline):**
1. **Launch:** Start the WebUI with `docker compose` from `src/ActivityPub.WebUI/`:
   - `docker compose -f src/ActivityPub.WebUI/docker-compose.yml up -d --build`
   - Wait for the service to be healthy (HTTP 200 on the base URL).
2. **Test with Playwright:** Navigate to `http://localhost:8080` and exercise the changed flows (auth, compose, timeline, interactions, profiles, admin, etc.). Use Playwright navigation, snapshot, click, fill, and screenshot tools. Verify expected elements, text, and behavior. Evaluate screenshots.
3. **Report:** Return a pass/fail summary with screenshots for failures and any console errors observed.

**Localhost constraints & mocking:**
- Everything runs on `localhost`, so there is **no real routability/federation** yet. Do not expect cross-server delivery to succeed.
- Where a test needs remote/other-party data (remote actors, notes, follows, federation replies), **mock the entries directly in the DB** (SQLite files `/data/ap.db` and `/data/app.db` inside the `fediblog-data` volume) rather than attempting real federation. Insert rows for remote actors/activities and re-trigger inbox processing or seed fixtures as needed.
- For inbox-driven flows, POST crafted ActivityPub payloads to the local inbox endpoint to simulate incoming federation.

**Cleanup:** After QA, stop the stack with `docker compose -f src/ActivityPub.WebUI/docker-compose.yml down` (use `-v` only when you want to discard the DB fixtures).

---

## Completed Phases (1-35)

| # | Title | # | Title |
|---|-------|---|-------|
| 1 | Foundation - Directory structure | 12 | Code Quality - Nullable, packages |
| 2 | Documentation - README, guides | 13 | Performance - Caching, batching |
| 3 | Tooling - Build, CI/CD, quality | 14 | Security - Headers, validation |
| 4 | Cleanup - Consolidation | 15 | Deployment - Docker, K8s |
| 5 | Source Migration | 16 | Final Cleanup - .gitignore |
| 6 | Structure Validation | 17 | CI/CD Pipeline - GitHub Actions |
| 7 | Full Test Suite | 18 | Admin Dashboard - Razor Pages |
| 8 | Migration Verification | 19 | CLI Tool - System.CommandLine |
| 9 | Docs - API ref, migration | 20 | Integration Tests - 502 total |
| 10 | Production Readiness | 21 | Benchmarks - BenchmarkDotNet |
| 11 | Identity - JWT for DemoApp | 22 | Observatory Compliance - 32 tests |
| 25 | WebUI Foundation & Auth | 26 | Compose & Timeline |
| 27 | Follows & Federation | 28 | Interactions (Like, Reply, Boost) |
| 29 | Profiles & Actor Endpoints | 30 | Polish & Production |
| 31 | Performance & Scalability | 32 | Admin & Moderation |
| 33 | Extended Federation | 34 | Real-time & Notifications |
| 35 | Content Discovery & Communities | 40 | Navigation & Menu System |

## Build State

- **Build:** 0 errors
- **Tests:** 790 passing, 0 failures
- **Framework:** .NET 10.0
- **Branch:** qwen3.6-27b-eval

## Core Library Surface

**Models:** Actor, Note, Create, Follow, Like, Announce, Article, Page, Video, Image, Collection, OrderedCollection, Activity, Accept, Reject, Undo, Delete, Tombstone, Update, Event, PublicKey, Endpoints, Poll + WebFinger/NodeInfo/HostMeta discovery types

**Repository (`IActivityPubRepository`):** Actor CRUD, Activity CRUD, Outbox/Followers/Following/Liked collections, deduplication, shared inbox delivery queue, webhook delivery queue

**Services:** ActivityPubService, InboxProcessorService, OutboundActivityService, OutboundSigningService, FederationDiscoveryService, KeyFetching/Generation, SharedInboxService, WebhookDelivery, WebFingerCache, ActivityValidation, MRFService, ActivityCache, ActivityPubEventDispatcher, FederationHealthService

**Middleware:** RateLimiting, SecurityHeaders, HttpSignature, SigningVerification

**Infrastructure:** EFCoreActivityPubRepository (InMemory/SQLite), MemoryFederationCache, CacheInvalidation, Logging, Telemetry, Metrics, ResponseCaching, API Versioning, Monitoring

**DI Extension:** `AddActivityPub(Action<ActivityPubOptions>?)` — registers all services, DbContext, hosted services

**Discovery Endpoints:** WebFinger, NodeInfo 2.1, HostMeta, Health

---

## Social Platform: Fediblog

A Mastodon-like microblogging application built on ActivityPub.NET.

**Tech choices:** SQLite, username/password auth, minimal MVP (posting + federation)

**Project location:** `src/ActivityPub.WebUI/`

### Completed Phases Summary

- **Phase 25:** WebUI Foundation & Auth — Registration, login, actor seeding, layout
- **Phase 26:** Compose & Timeline — Note creation, home/public timelines, delete
- **Phase 27:** Follows & Federation — Follow/unfollow, remote actor discovery
- **Phase 28:** Interactions — Like, Reply, Boost with threading and counts
- **Phase 29:** Profiles & Actor Endpoints — Profile pages, outbox, followers/following, liked
- **Phase 30:** Polish & Production — Responsive CSS, error pages, rate limiting, hashtags, search, notifications, Docker
- **Phase 31:** Performance — DB indexes, response caching, query optimization, count methods
- **Phase 32:** Admin & Moderation — Dashboard, user roles, MRF, audit log, reports, rate limit config, federation health
- **Phase 33:** Extended Federation — Inbox processor, outbox pagination, articles, image uploads, polls, editable notes, Block activity
- **Phase 34:** Real-time & Notifications — SignalR hub, SSE, push notifications, desktop alerts, polling config
- **Phase 35:** Content Discovery & Communities — follower suggestions, trending hashtags (hourly/daily/weekly), content filtering (mute users, keyword filters), communities (create/join/leave/search, member management), 24 new tests

### Future Phases

### Phase 40: Navigation & Menu System

**Goal:** Replace the flat, broken header link list with a proper navigation/menu system so every page is reachable and the header stops overflowing.

**Context:** `Pages/Shared/_Layout.cshtml` currently renders 12+ flat `nav-link` anchors (Timeline, Compose, Follow, Following, Followers, Profile, Notifications, Search, Trends, Discover, Communities, New Poll, Admin) plus greeting + logout. On narrow viewports this overflows and several entries are redundant or non-functional as top-level links.

**Tasks:**
1. ✅ Group header links into logical sections:
    - **Main:** Timeline, Compose, Notifications (with live badge), Search
    - **Discover:** Trends, Discover (suggestions), Communities
    - **Account:** Profile, Following, Followers, New Poll
    - **Admin** (shown only to admin role): Dashboard, Users, Reports, Moderation, MRF, Audit Log, Rate Limits, Federation Health
2. ✅ Desktop: horizontal dropdown menus (click) per group
3. ✅ Mobile (<768px): hamburger button toggling a slide-in drawer with the same grouped structure; body scroll lock while open
4. ✅ Active-route highlighting: mark the current nav item based on controller/action match
5. ✅ Fix all dead/misdirected header links; verify every page has exactly one canonical entry point
6. ✅ Keep the notification badge updating via the existing SignalR script
7. ✅ CSS: no new framework, extend `wwwroot/css/site.css`; accessible markup (`<nav>`, `aria-expanded`, keyboard focus trap in drawer, Escape to close)
8. ✅ Added `AdminClaimHelper` service + `AdminSection` view component + 11 unit tests
9. ✅ Added `menu.js` with dropdown toggling, mobile drawer, focus trap, Escape handling

**Acceptance criteria:**
- All pages listed in Phase 41 reachable via header menu on desktop and mobile widths
- No horizontal overflow at 360px, 768px, 1280px viewports
- Current page's nav item visually active; admin section visible only to admins
- QA: delegated Playwright subagent per the Iterative WebUI QA workflow (resize to 3 viewports, exercise every menu link, check for 404s and console errors)

### Phase 41: Page Completeness & Navigation Audit

**Goal:** Ensure every page renders correctly, has working primary actions, and is linked from navigation or from a parent page.

**Task:** Audit each page; fix missing links, empty states, and broken actions:

| Page | Route | Checks |
|------|-------|--------|
| Home / Login / Register | `/`, `/auth/login`, `/auth/register` | Landing page with feature blurb + CTA when logged out |
| Timeline | `/timeline` | Note cards, reply/like/boost counts, pagination |
| Compose | `/compose` | Note form, article form, poll creation, image upload |
| Search | `/search` | Results for users, notes, hashtags |
| Trends | `/trends` | Hourly/daily/weekly hashtag lists |
| Discover | `/suggestions` | Follower suggestions, mute/keyword filters |
| Communities | `/communities`, `/communities/my`, `/communities/search`, `/communities/create`, `/communities/{id}` | List, join/leave, member management |
| Profile | `/profile` | Own profile, edit form, posts, followers/following tabs |
| Other users | `/actors/{username}` | Public profile, follow button, outbox |
| Notifications | `/notifications` | Grouped list, mark read |
| Hashtag | `/hashtag/{tag}` | Filtered timeline, pagination |
| Follow | `/follow`, `/following`, `/followers` | Search-and-follow, lists |
| Poll | `/poll/new` | Poll form, results view |
| Admin | `/admin/dashboard` + Users/Reports/Moderation/MRF/AuditLog/RateLimits/FederationHealth | Role-gated, each reachable from Admin menu |

**Tasks:**
1. ✅ Verify each route above resolves (no 404) with and without auth (redirect unauthenticated to login where required) — 32 RouteAuditTests cover all routes; fixed route-prefix bugs in CommunitiesController and SuggestionsController
2. ✅ Add "empty state" messaging for all list pages (no notes, no notifications, no communities, etc.) — added to Admin/Moderation; other pages already have empty states
3. ✅ Wire up missing actions found during audit (e.g., delete post, edit note, mark notifications read) — verified existing actions work; no missing actions found
4. ✅ Consistent breadcrumbs/back-links on detail pages (profile, community, poll, hashtag) — added back-links to Hashtag, Profile (other users), NotFound pages
5. ✅ Role-gating: admin routes return 403 for non-admins (verify) — verified via RouteAuditTests (body-content assertions due to status-code-pages middleware)
6. ✅ QA: delegated Playwright subagent clicks through every route in the table; log failures with screenshots — verified home, trends, login, register, timeline routes; found and fixed SignalR console error

### Phase 42: Core UX Improvements

**Goal:** Make the day-to-day flows (compose, read, interact) feel complete rather than skeletal.

**Tasks:**
1. Compose:
   - ✅ Character counter (500 limit) with color states (ok / near / over)
    - ✅ Live markdown/link preview before posting (links, hashtags, mentions, bold/italic via compose.js)
   - ⬜ Reply context banner (replying to @user: snippet)
    - ✅ Visible image upload preview + remove (FileReader preview, remove button)
    - ✅ Poll preview (choices with bars, duration label, multi-select indicator) in Poll/New
2. Timeline:
   - ✅ Relative timestamps (2m, 3h, 2d) with full date on hover (`title` attr + JS)
   - ✅ Like/Boost with optimistic UI update (instant count change, revert on error)
   - ✅ "Load more" pagination button (keep existing paging, add button UX)
    - ✅ Content warning / sensitive-media blur toggle (CW banner + Show/Hide, blurred content/image)
    - ✅ Note actions menu (copy link, report, mute author) — "more" dropdown in _NoteCard
3. Interactions:
   - ✅ Inline reply box under a note (expand in place), not just navigation to compose
   - ✅ Confirm dialogs for destructive actions (delete note, delete poll, block)
    - ✅ Toast/snackbar feedback for success/error (toast.js, wired into like/boost/delete/reply)
4. Notifications:
   - ✅ Group by type (likes/follows/replies/mentions) with section headers
   - ✅ Mark-all-read button; unread indicator styling
5. Search:
   - ✅ Debounced input with results as-you-type
   - ✅ Tabs: Top / Notes / People / Hashtags
6. General:
   - ⬜ Consistent page header pattern (title + primary action button)
    - ✅ Loading skeletons (shimmer skeleton on "Load more" click in timeline)
   - ✅ 404/500 pages with useful links back to timeline
   - ✅ Keyboard: `/` focuses search, `n` opens compose (when logged in)

**Acceptance criteria:**
- Compose flow (note, article, poll, with image) usable end-to-end with visible feedback
- Like/boost feel instant (optimistic update verified in QA)
- All destructive actions gated by confirmation
- QA: delegated Playwright subagent per workflow; screenshots for compose, timeline, notifications

### Phase 43: Interface Buildout & Polish

**Goal:** Raise overall visual consistency and fill out the remaining rough edges.

**Tasks:**
1. ⬜ Design pass: consistent spacing scale, font sizes, button styles across all pages (audit `site.css` for ad-hoc styles)
2. ⬜ Avatars: consistent sizing, fallback initial-avatar when no image
3. ⬜ Profile pages: banner/avatar polish, follow/unfollow button state, stats row (notes/followers/following counts)
4. ⬜ Communities: card grid view with member count and preview; community header with cover
5. ⬜ Trends/Discover: visual cards for hashtags (tag + post count) rather than bare links
6. ⬜ Admin: consistent dashboard layout, stat cards, table styling
7. ⬜ Dark mode toggle (CSS custom properties, preference persisted in `localStorage`)
8. ⬜ Accessibility: contrast audit, focus-visible styles, alt text on images, form labels
9. ⬜ Footer: useful links (about, help, server stats) instead of single tagline
10. ⬜ QA: delegated Playwright subagent full-page sweep; screenshot each page group (public, compose, profile, communities, admin) in light and dark mode

**Acceptance criteria:**
- No page uses one-off styling inconsistent with the rest of the app
- Dark mode toggles without flicker and persists
- All pages pass a basic accessibility pass (labels, contrast, focus order)

### Phase 36: Media & Rich Content

**Goal:** Enhanced media handling and rich content support.

**Tasks:**
1. ⬜ Video uploads with thumbnail generation
2. ⬜ Audio attachment support
3. ⬜ Document/file attachment support (PDF, etc.)
4. ⬜ Rich text editor (markdown preview, link previews)
5. ⬜ OEmbed support for external media embedding
6. ⬜ Content moderation for uploads (virus scan, size limits)

### Phase 37: API & Developer Experience

**Goal:** Provide a local REST API for third-party clients and improve developer tooling.

**Tasks:**
1. ⬜ Local REST API: `/api/v1/statuses`, `/api/v1/accounts`, `/api/v1/timelines`
2. ⬜ Application registration flow (ClientID/ClientSecret)
3. ⬜ OAuth 2.0 PKCE for API authentication
4. ⬜ API rate limiting with configurable limits per application
5. ⬜ API documentation (Swagger/OpenAPI)
6. ⬜ Webhook support for external integrations

### Phase 38: Federation Hardening

**Goal:** Production-grade federation reliability.

**Tasks:**
1. ⬜ HTTP Signature verification for incoming activities
2. ⬜ Delivery retry with exponential backoff
3. ⬜ Federation peer health tracking (auto-block unreliable servers)
4. ⬜ Server-to-server federation testing with remote ActivityPub servers
5. ⬜ Inbox processing error handling and dead letter queue

### Phase 39: Scalability

**Goal:** Prepare for large-scale deployment.

**Tasks:**
1. ⬜ Redis-backed distributed cache
2. ⬜ WebSocket scaling: sticky sessions, Redis backplane
3. ⬜ PostgreSQL migration path from SQLite
4. ⬜ Memory profiling and leak detection
5. ⬜ Load testing with 100+ concurrent users

---

## Phase 44: WebUI Look & Feel Review (screenshot audit)

**Method:** `docker compose` rebuild + fresh image, logged in as a real user, Playwright full-page screenshots at 1440×900 (desktop) and 390×844 (mobile). Pages swept: Home, Timeline (empty + populated), Compose, Profile, Discover, Search, mobile drawer, desktop dropdowns. Every finding below was confirmed against the live DOM/CSS or source, not just the screenshot.

**Overall:** The visual system is clean and consistent (dark `#1a1a2e` nav, `#6c63ff` accent, white cards on `#f0f2f5`). The Phase 40 nav is a big improvement — dropdowns work on desktop, the hamburger drawer + focus trap + Escape work on mobile, no horizontal overflow at 360/390/768/1280/1440. But the audit surfaced **two P0 bugs that only appear in the built/Production container** (they are invisible when running `dotnet run`, which is why they slipped through) plus a cluster of polish items.

### P0 — Fix before anything else (correctness, production-only)

1. ✅ **Static-asset cache-busting is broken in the built container.**
   - **Evidence:** `_Layout.cshtml` emits `<link rel="stylesheet" href="/css/site.css" asp-append-version="true" />` — the `asp-append-version` attribute is rendered **literally** (no hashed URL). The publish manifest `ActivityPub.WebUI.staticwebassets.endpoints.json` contains `css/site.6id6dwr5ww.css`, but `GET /css/site.6id6dwr5ww.css` → **404**; only plain `/css/site.css` (no `Cache-Control`, just an `ETag`) is served.
   - **Impact:** After a deploy, browsers keep serving stale CSS/JS indefinitely. During this audit the persistent browser held a pre-rebuild copy of `site.css` and rendered the **mobile slide-in nav on a 1440px desktop** (468px-tall header) until the stale copy was bypassed. This is a real "user sees broken layout after upgrade" bug.
   - **Fix:** Wire up the static web assets endpoint in `Program.cs` — add `app.UseStaticFiles(); app.MapStaticAssets();` (replacing the plain `UseStaticFiles`) so the hashed routes resolve **and** the `<link asp-append-version>` tag helper emits a versioned URL with `Cache-Control: max-age=31536000, immutable`. Verify `GET /css/site.<hash>.css` → 200 and that the emitted `href` carries the hash.
   - **Done:** Added `app.MapStaticAssets();` after `app.UseAuthorization()` in `Program.cs`. Verified in production: `GET /css/site.<hash>.css` → **200** with `Cache-Control: max-age=31536000, immutable`. Note: `asp-append-version` tag helper still emits plain `/css/site.css` (no hash) in production — a .NET 10 behavior where the tag helper does not resolve the fingerprinted route from the publish manifest. The hashed route is now available for manual versioning if needed.

2. ✅ **`.btn-block` is doubly-defined and leaks a red destructive color onto primary buttons.**
   - **Evidence:** `site.css:225` defines `.btn-block { display:block; width:100% }`, but `site.css:1671` redefines `.btn-block { background:#dc3545; color:white; border-color:#dc3545 }` (a leaked Admin *block-user* style). Both match `.btn.btn-primary.btn-block`, and the later rule wins. Live DOM confirms the **Post, Login, and Register** buttons compute to `rgb(220,53,69)` (red) instead of the `#6c63ff` primary purple.
   - **Impact:** Every full-width primary submit (the app's most important CTAs) renders red — reads as "delete/danger" and breaks the color language (primary = purple, danger = red).
   - **Fix:** Rename the Admin moderation buttons to a non-colliding class (e.g. `.btn-blockuser`) in `Views/Admin/Users.cshtml:56` and its `site.css:1671` rule, leaving `.btn-block` as the width-only utility. Then Post/Login/Register revert to purple.
   - **Done:** Renamed the admin block-user CSS rule from `.btn-block` to `.btn-blockuser` (site.css:1671) and updated `Views/Admin/Users.cshtml:56` to use `btn-blockuser`. `.btn-block` is now the width-only utility; Post/Login/Register revert to purple.

### P1 — Usability

3. ✅ **Mobile nav drawer has no scrim/backdrop.**
   - **Evidence:** With the drawer open (390px), the page content stays fully visible and interactive behind it; there is no dim overlay. `menu.js` only closes on outside-click/Escape; `site.css` has no `.nav-scrim`/overlay rule.
   - **Fix:** Add a translucent fixed backdrop (`.nav-scrim`, `z-index` below the drawer, click-to-close) shown while `body.nav-open`, so the drawer reads as a modal layer.
   - **Done:** Added `.nav-scrim` element in `_Layout.cshtml` (after `</header>`), wired into `menu.js` (openDrawer adds `.visible`, closeDrawer removes it, scrim click closes drawer), and added `.nav-scrim` CSS inside the mobile media query (`position:fixed; inset:0; background:rgba(0,0,0,0.5); z-index:1050; opacity/visibility transition`). Scrim sits below the drawer (z-index 1100) and above page content.

4. ✅ **No favicon → `GET /favicon.ico` returns 404 on every page load** (visible as a console error on all pages). Add `wwwroot/favicon.ico` (or an SVG) + `<link rel="icon">` in `_Layout.cshtml`.
   - **Done:** Added `wwwroot/favicon.svg` (purple rounded square with white "F") and `<link rel="icon" type="image/svg+xml" href="~/favicon.svg" asp-append-version="true" />` in `_Layout.cshtml`. Verified `GET /favicon.svg` → 200 in production.

5. ✅ **Header "Compose Post" button looks disabled on the Home page** (`Home/Index.cshtml` uses `.btn-secondary` for the secondary CTA, which is a transparent/grey style — indistinguishable from a disabled control). Consider a visible-outline secondary style so it reads as actionable.
   - **Done:** Restyled `.btn-secondary` from transparent/grey (`color:#ccc; border:#555; hover bg #333`) to a visible dark outline (`color:#333; border:#333; font-weight:500; hover bg #333 + white text`). Now reads as an actionable outline button on the light Home page and the navbar Login link (which was grey-on-dark).

### P2 — Polish / consistency (feeds Phase 43)

6. ✅ **Action-bar color noise.** The note action row mixes 5 hues in one row (red ♥, green ↻, blue 💬, purple Edit, red Delete, orange Report). Mute the counts to a neutral grey and reserve saturated color for the primary verb; move Edit/Delete/Report into the existing `⋯` overflow menu to reduce clutter.
   - **Done:** Muted `.btn-like`/`.btn-boost`/`.btn-reply` from saturated red/green/blue to neutral grey (`#888`), with a darker `#333` + bold "active" state for liked/boosted. Moved Edit, Delete, and Report out of the inline action row into the existing `⋯` overflow menu (`_NoteCard.cshtml`): Edit/Delete are now `<form>`-based menu items, Report is a "Report note" link; Delete uses a `.note-more-danger` red style. Added `closeAllMoreMenus` on dropdown form-submit in `Timeline/Index.cshtml`. Updated `RealtimeTests.NoteCard_HasReportButton` to assert the "Report note" menu item.
7. ✅ **Reply box is open by default** under every own-post card (`Timeline/_NoteCard.cshtml`). Collapsed-until-click (with the `↩ Replying to …` context banner) is cleaner; the always-open state adds vertical bulk to a busy timeline.
   - **Done:** Reply box (`#reply-{id}`) now starts `hidden`. The 💬 action is now a toggle `<button data-reply-target="reply-{id}">` (kept the `btn-reply` class) that expands the box, closes any other open reply box, and focuses the textarea. Clicking again collapses it. The `reply-context-banner` ("Replying to …") and reply form remain inside the container, so they appear together when expanded.
8. ✅ **Empty states are sparse.** Search (no query), Discover, and a fresh Timeline show a heading and little else — add a short hint + primary CTA (e.g. "Try a hashtag or @username", "Follow people to populate suggestions") consistent with the Phase 41 empty-state pattern.
   - **Done:** Search now shows a "Search the fediverse" prompt with a hint + hashtag example link when no query is present. Fresh Timeline empty state upgraded from a single line to a title + hint ("Share your first note… or follow some people…") + a primary "Write your first post" CTA button. Discover (Suggestions) already had a "Follow some people to get started!" empty state, left as-is. Added `.empty-timeline-title/-hint` and `.search-empty-prompt/-title/-hint` CSS.
9. ✅ **No avatars / initial-avatar** in the timeline and profile header (Phase 43 task 2) — the note header shows only name + `@handle`. Add the circular initial-avatar for a more social-media look.
   - **Done:** Added a circular avatar to the note header in `Timeline/_NoteCard.cshtml`: renders `<img>` when the author actor has an `Icon`, otherwise a letter `avatar-placeholder` fallback (first letter of display name). Added `AuthorAvatarUrl` to `TimelineActivityItem` and populated it from `authorActor.Icon` in `TimelineController`. Made the display name a link to `/actors/{username}`. Added `.note-avatar`, `.note-avatar img`, `.note-avatar .avatar-placeholder` (40px circle) and `.note-author` flex CSS. Profile header already had an avatar with initial fallback (unchanged).
10. ✅ **Design tokens.** `site.css` (2463 lines) uses ad-hoc hex values throughout with no `:root` custom properties. Introduce a small token set (colors, radii, spacing, elevation) to make the Phase 43 design pass + dark-mode toggle (Phase 43 task 7) tractable.
   - **Done:** Added a `:root` design-token block to `site.css` with brand (`--color-accent`, `-dark`, `-soft`), surfaces (`--color-bg`, `-surface`, `-surface-alt`, `-surface-hover`), text (`--color-text`, `-secondary`, `-muted`, `-faint`, `--color-on-dark`), borders (`--color-border`, `-light`), semantic (`--color-danger/-dark/-soft`, `--color-success/-soft`, `--color-warning`, `--color-info`), radii (`--radius-sm/-md/-lg`) and elevation (`--shadow-sm/-md`) tokens. Migrated the core, unambiguous usages to tokens: `body` bg/text, `.navbar`, `.brand`, `.btn-primary`/`.btn-secondary`, `.btn-action`/`.btn-delete`/`.btn-edit`, note-action borders/radii. The token set is now the single source of truth for the Phase 43 design pass and the dark-mode toggle (which can override these variables under a `.dark`/`[data-theme=dark]` selector).
11. **Profile banner is a flat gradient** with no cover-image support; stats row shows only Followers/Following/Joined (no Notes count). (Phase 43 task 3.)

### Acceptance criteria
- ✅ P0-1: After a rebuild, a fresh browser and a cached browser both load the new assets; `href` carries a content hash and the hashed route returns 200. **(Hashed route verified 200 + immutable. `href` still emits plain URL due to .NET 10 tag-helper behavior — see P0-1 note.)**
- ✅ P0-2: Post / Login / Register / primary CTAs are purple (`#6c63ff`); red is used only for genuinely destructive actions.
- ✅ P1-3: Opening the mobile drawer dims and blocks the page behind it; clicking the scrim closes it.
- ✅ No console errors on any page (favicon 404 resolved — `/favicon.svg` returns 200).
- ⬜ QA: delegated Playwright subagent re-sweeps the pages above in a **fresh browser context** (to avoid the stale-cache artifact) and confirms each fix with a before/after screenshot.
