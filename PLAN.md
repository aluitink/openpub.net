# ActivityPub.NET - Project Plan

**Last Updated:** Aug 16, 2026
**Status:** Phases 1-41 complete. 797/797 tests passing. Phase 42 done (2 tasks deferred to Phase 43). Phase 44 P0/P1/P2 fixes + inline reply compose applied (fresh-context QA re-sweep outstanding). Phase 45 complete (all code under `src/`). Phase 43 in progress: design-token pass (T1), dark mode (T7), page-header pattern (T10), **avatars (T2)** done; **critical nav bug fixed** (layout moved `Pages/Shared/` → `Views/Shared/` so MVC tag helpers generate hrefs — all menu links now work). T2 also fixed a real EF `ToLowerInvariant` bug that broke all user search and a silent `[Required]` bug that blocked every profile save.

---

## Testing Guidelines

- **Browser/UI tests using Playwright** should be executed in delegated agents rather than inline, to isolate browser state and avoid conflicts with the main session.

## Public Deployment / Integration Test Host

The local Docker container for the WebUI is exposed publicly at:

**https://openpub.luit.ink/**

Configure and launch the stack for this public hostname (not just `localhost`) so we can test real-world integrations against a routable address — most importantly **following real users on other ActivityPub servers**, plus WebFinger, HTTP signatures, and external clients:

1. **Docker compose** (`src/ActivityPub.WebUI/docker-compose.yml`): add the public host as a server name so the container serves and identifies as `openpub.luit.ink`.
2. **ActivityPub domain**: set `ActivityPub:Domain` to `https://openpub.luit.ink` (via env var / config) so actor IDs, WebFinger, NodeInfo, and outbound federation use the public URL instead of `localhost`.
3. **TLS**: the reverse proxy terminates HTTPS on `443`; map the container's `443` port (already exposed as `8443:443`) or adjust the proxy accordingly.
4. Use this host for the Phase 38 federation testing and any real server-to-server integration work.
5. **Real-world test target:** a real user we can follow and interact with for federation testing is **@RayvenMX@mastodon.world** — use them for follow/unfollow, reply, like, and inbox-delivery checks against a live server.

## Iterative WebUI QA (Delegated Subagents)

When making changes to `src/ActivityPub.WebUI/`, run QA via delegated subagents using Playwright tools. Do this after each meaningful change batch, before marking a phase/feature complete.

**Workflow (delegate to a subagent, not inline):**
1. **Launch:** Start the WebUI with `docker compose` from `src/ActivityPub.WebUI/`:
   - `docker compose -f src/ActivityPub.WebUI/docker-compose.yml up -d --build`
   - Wait for the service to be healthy (HTTP 200 on the base URL).
2. **Test with Playwright:** Navigate to `http://localhost:8080` (or `https://openpub.luit.ink/` when validating federation) and exercise the changed flows (auth, compose, timeline, interactions, profiles, admin, etc.). Use Playwright navigation, snapshot, click, fill, and screenshot tools. Verify expected elements, text, and behavior. Evaluate screenshots.
3. **Report:** Return a pass/fail summary with screenshots for failures and any console errors observed.

**Localhost constraints & mocking:**
- When QA runs against `localhost` there is **no real routability/federation** — do not expect cross-server delivery to succeed in that mode. (When the stack is pointed at the public host `https://openpub.luit.ink/`, real federation against remote servers — including following real users — is possible; see *Public Deployment / Integration Test Host*.)
- Where a test needs remote/other-party data (remote actors, notes, follows, federation replies), **mock the entries directly in the DB** (SQLite files `/data/ap.db` and `/data/app.db` inside the `fediblog-data` volume) rather than attempting real federation. Insert rows for remote actors/activities and re-trigger inbox processing or seed fixtures as needed.
- For inbox-driven flows, POST crafted ActivityPub payloads to the local inbox endpoint to simulate incoming federation.

**Cleanup:** After QA, stop the stack with `docker compose -f src/ActivityPub.WebUI/docker-compose.yml down` (use `-v` only when you want to discard the DB fixtures).

---

## Build State

- **Build:** 0 errors
- **Tests:** 798 passing, 0 failures
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

## Completed Phases

### Core library (Phases 1-22)

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

### Fediblog WebUI (Phases 25-41)

A Mastodon-like microblogging app built on ActivityPub.NET. SQLite, username/password auth. Location: `src/ActivityPub.WebUI/`.

| # | Title | # | Title |
|---|-------|---|-------|
| 25 | WebUI Foundation & Auth — registration, login, actor seeding, layout | 33 | Extended Federation — inbox processor, outbox pagination, articles, image uploads, polls, editable notes, Block |
| 26 | Compose & Timeline — note creation, home/public timelines, delete | 34 | Real-time & Notifications — SignalR hub, SSE, push notifications, desktop alerts |
| 27 | Follows & Federation — follow/unfollow, remote actor discovery | 35 | Content Discovery & Communities — suggestions, trending hashtags, content filtering, communities |
| 28 | Interactions — Like, Reply, Boost with threading and counts | 40 | Navigation & Menu System — grouped dropdowns, mobile drawer, active-route highlight |
| 29 | Profiles & Actor Endpoints — profile pages, outbox, followers/following, liked | 41 | Page Completeness & Navigation Audit — 32 RouteAuditTests, empty states, back-links, role-gating |
| 30 | Polish & Production — responsive CSS, error pages, rate limiting, hashtags, search, Docker | 42 | Core UX Improvements — char counter, previews, optimistic like/boost, toasts, skeletons (inline reply completed in Phase 44 P1; 2 other tasks deferred to Phase 43) |
| 31 | Performance — DB indexes, response caching, query optimization | 44 | WebUI Look & Feel Review — screenshot audit; P0/P1/P2 fixes applied (fresh-context QA re-sweep outstanding) |
| 32 | Admin & Moderation — dashboard, roles, MRF, audit log, reports, federation health | 43 | (open — see Open Work) |
| 45 | Consolidate All Code Under `src/` — Tests, Benchmarks, samples moved; solution, scripts, CI, docs updated | | |

**Phase 44 fixes applied (summary):** P0-1 static-asset cache-busting wired via `app.MapStaticAssets()` (hashed routes 200 + immutable; `asp-append-version` still emits plain URLs — .NET 10 tag-helper behavior). P0-2 admin block-user style renamed `.btn-block` → `.btn-blockuser` so primary CTAs are purple again. P1 mobile drawer scrim, favicon (`/favicon.svg`), secondary-button outline restyle, **inline reply compose** (`/compose?replyTo=…` with reply-context banner, hidden `InReplyTo`, reply sets `Note.InReplyTo` + queues shared-inbox delivery to the target actor; note-card reply links now route to it; `ReplyComposeTests` added, 797 tests passing). P2 muted action-bar colors + Edit/Delete/Report moved to overflow menu, collapsed-by-default reply boxes, richer empty states, note-card avatars, `:root` design-token block in `site.css`.

---

## Open Work

### Phase 43: Interface Buildout & Polish

**Goal:** Raise overall visual consistency and fill out the remaining rough edges.

**Tasks:**
1. ✅ Design pass: consistent spacing scale, font sizes, button styles across all pages (audit `site.css` for ad-hoc styles; the `:root` design-token block from Phase 44 is the starting point) — *type/spacing/component-metric tokens added and applied*
2. ✅ Avatars: consistent sizing, fallback initial-avatar when no image — *all surfaces standardized (notes 40px, search/suggestions 48px, profile 90px) via a `--avatar-size` token + `.avatar-placeholder-lg`; fixed `Image.ToString()` bug (used `.Url`), added missing Suggestions fallback, synced identity `AvatarUrl` on profile edit, fixed EF `ToLowerInvariant` search bug + silent `[Required]` save failures*
3. ✅ Profile pages: banner/avatar polish, follow/unfollow button state, stats row (notes/followers/following counts) — *added `GetNoteCountAsync` + `IsFollowingAsync` (Undo-aware) to repo; Profile page shows Notes/Followers/Following stats + Follow/Following toggle button (btn-primary/btn-secondary) for other users, Edit for own; new `Profile/Follow` + `Profile/Unfollow` POST endpoints (Undo + delete for unfollow); fixed broken `Profile/{username}` path by switching to `[Route("Profile")]` with `?username=` query param; Dockerfile now ships `sqlite3`*
4. ✅ Communities: card grid view with member count and preview; community header with cover — *typed `Community.Icon`/`Image` to `Image?`; added `UpdateCommunityAsync` to `ICommunityService`/`CommunityServiceImpl` (JSON re-serialize + entity update); create form accepts optional `IconUrl`; Index/MyCommunities/Search render community cards (cover banner + icon w/ initial fallback + member count + owner/member badges); Show has a full header (cover + icon + summary + member count + join/leave/delete actions); CSS added incl. dark-mode overrides*
5. ⬜ Trends/Discover: visual cards for hashtags (tag + post count) rather than bare links
6. ⬜ Admin: consistent dashboard layout, stat cards, table styling
7. ✅ Dark mode toggle (CSS custom properties, preference persisted in `localStorage`; token overrides under `[data-theme=dark]`) — *full dark palette + `[data-theme=dark]` overrides, toggle button + `localStorage` persistence in layout; mobile hamburger bars + hover states fixed*
8. ⬜ Accessibility: contrast audit, focus-visible styles, alt text on images, form labels — *Phase 44 P2-11: several icon-only buttons lack `aria-label`/`title` (e.g. note-card overflow menu trigger), some decorative icons not `aria-hidden`*
9. ⬜ Footer: useful links (about, help, server stats) instead of single tagline
10. ✅ Consistent page header pattern (title + primary action button) — *all 20 `.page-header` views standardized: title left (unified `<h1>`), actions right in `.page-header-actions`; CSS added; deferred from Phase 42*

**Acceptance criteria:**
- No page uses one-off styling inconsistent with the rest of the app
- Dark mode toggles without flicker and persists
- All pages pass a basic accessibility pass (labels, contrast, focus order)

### Phase 44 (remaining): Fresh-context QA re-sweep

- ⬜ Delegated Playwright subagent re-sweeps the audited pages in a **fresh browser context** (to avoid the stale-cache artifact) and confirms each fix with a before/after screenshot.

### Phase 36: Media & Rich Content

**Goal:** Enhanced media handling and rich content support.

1. ⬜ Video uploads with thumbnail generation
2. ⬜ Audio attachment support
3. ⬜ Document/file attachment support (PDF, etc.)
4. ⬜ Rich text editor (markdown preview, link previews)
5. ⬜ OEmbed support for external media embedding
6. ⬜ Content moderation for uploads (virus scan, size limits)

### Phase 37: API & Developer Experience

**Goal:** Provide a local REST API for third-party clients and improve developer tooling.

1. ⬜ Local REST API: `/api/v1/statuses`, `/api/v1/accounts`, `/api/v1/timelines`
2. ⬜ Application registration flow (ClientID/ClientSecret)
3. ⬜ OAuth 2.0 PKCE for API authentication
4. ⬜ API rate limiting with configurable limits per application
5. ⬜ API documentation (Swagger/OpenAPI)
6. ⬜ Webhook support for external integrations

### Phase 38: Federation Hardening

**Goal:** Production-grade federation reliability. (Use the public host `https://openpub.luit.ink/` and @RayvenMX@mastodon.world for task 4.)

1. ⬜ HTTP Signature verification for incoming activities
2. ⬜ Delivery retry with exponential backoff
3. ⬜ Federation peer health tracking (auto-block unreliable servers)
4. ⬜ Server-to-server federation testing with remote ActivityPub servers
5. ⬜ Inbox processing error handling and dead letter queue

### Phase 39: Scalability

**Goal:** Prepare for large-scale deployment.

1. ⬜ Redis-backed distributed cache
2. ⬜ WebSocket scaling: sticky sessions, Redis backplane
3. ⬜ PostgreSQL migration path from SQLite
4. ⬜ Memory profiling and leak detection
5. ⬜ Load testing with 100+ concurrent users

### Phase 45: Consolidate All Code Under `src/` — COMPLETE

**Goal:** Move the remaining top-level code projects into `src/` so `src/` is the single home for all code (production, tests, benchmarks, samples), leaving only docs, config, scripts, and CI at the repo root.

**Done (commit a880e37 + 062075f):**
1. ✅ Moved `ActivityPub.Tests/` → `src/ActivityPub.Tests/` (git mv, history preserved)
2. ✅ Moved `ActivityPub.Benchmarks/` → `src/ActivityPub.Benchmarks/` and added it to `ActivityPub.sln` (newly included, nested under the `src` solution folder)
3. ✅ Moved `samples/` → `src/samples/` (internal structure kept: BotApp, ConsoleClient, DemoApp, FederationApp, quickstart, advanced)
4. ✅ `ActivityPub.sln`: all project paths fixed; Benchmarks project + 12 config entries added; Tests/Benchmarks nested under `src`
5. ✅ Fixed `ProjectReference` paths in Tests, Benchmarks, and all 4 sample csproj files
6. ✅ `scripts/build.sh` + `scripts/test.sh` point at `src/…` paths
7. ✅ `.github/workflows/ci.yml`: build-artifact path → `src/ActivityPub.Tests/bin/Release/`
8. ✅ Docs updated: README (directory tree + `cd src/samples/quickstart`), directory-structure, testing, overview, state-report
9. ✅ Verified: `dotnet build` 0 errors, `dotnet test` 798/798 passing, `scripts/build.sh` + `scripts/test.sh` succeed end-to-end
