# ActivityPub.NET - Project Plan

**Last Updated:** Aug 17, 2026
**Status:** Phases 1-45 complete. **911/911 tests passing.** Phase 43 (Interface Buildout & Polish) **complete** — all 10 tasks done. Phase 37 T1 done: local Mastodon-compatible REST API under `/api/v1` (statuses/accounts/timelines, numeric status IDs, cookie-session auth, 10 API tests). Phase 37 T2 done: application registration (`POST`/`GET /api/v1/apps`) issuing ClientID/ClientSecret, backed by `OAuthClientEntity` + `IApplicationRepository` (5 API tests). Phase 37 T3 done: OAuth 2.0 PKCE for API auth (`/api/v1/oauth/authorize` + `/api/v1/oauth/token`, Bearer `BearerToken` scheme, 7 API tests). Phase 37 T4 done: API rate limiting per application (`ApiRateLimiter` + `ApiRateLimitingMiddleware`, `RateLimit-*` headers, 429, configurable + per-client_id overrides, 10 tests). Phase 37 T5 done: API docs — Swashbuckle Swagger UI at `/swagger` + OpenAPI JSON at `/swagger/v1/swagger.json`, Bearer+cookie security schemes, XML doc comments (3 tests). Phase 37 T6 done: webhook support — CRUD `POST/GET/DELETE /api/v1/webhooks` (HMAC secret), `post.created` deliveries queued on new posts, background delivery service with retries (8 tests). Phase 44 P0/P1/P2 fixes + inline reply compose applied (fresh-context QA re-sweep outstanding). Phase 38 T1 done: HTTP signature verification for incoming activities — rewrote `HttpSignatureMiddleware` (raw-bytes RSA-SHA256/PKCS#1 verify, replay + digest checks, options-driven posture), fixed `OutboundSigningService` + `KeyFetchingService`, wired into WebUI (16 tests). Phase 38 T2 done: delivery retry with exponential backoff — `DeliveryRetryOptions` + `NextRetryAt` backoff gate, config-driven retry in `SharedInboxService` (fixed latent bug where retries never re-attempted), 7 tests. Phase 38 T3 done: federation peer health tracking + auto-block — `FederationPeerEntity`/`PeerHealthOptions`/`IPeerHealthService`, auto-block on consecutive delivery failures & probe unreachability, auto-unblock on consecutive successes, outbound skip + inbound reject for blocked peers, `PeerHealthBackgroundService` periodic probes, 14 tests. Phase 38 T4 done: server-to-server federation testing — fixed critical outbound bugs (empty private key in `ProcessQueueAsync`, missing `created` signature parameter, inbox-URL HEAD-probing deadlock, WebFinger self-href missing scheme, inconsistent key ID), added `appsettings.json` to WebUI, 4 end-to-end sign→verify round-trip tests. Phase 38 T5 done: inbox processing error handling + dead letter queue — `InboxDeadLetterEntity`/`InboxDeadLetterStatus`, `InboxProcessingOptions` (retry + backoff + DLQ), `SharedInboxService.ProcessAndDistributeActivityAsync(username, activity, rawJson)` retry loop with `ProcessAndDistributeCoreAsync` single-attempt pipeline, `HandleInboxDeadLetterAsync` persists raw payload on exhaustion, `ProcessInboxDeadLettersAsync(batchSize)` replay, `InboxDeadLetterBackgroundService` periodic reprocess + prune, `ActorController.PostInbox` raw-body capture (reset position after `[FromBody]`), 400/500 semantics, admin `DeadLetterCount` card, 23 new tests.

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
5. ✅ Trends/Discover: visual cards for hashtags (tag + post count) rather than bare links — *replaced bare `<ol>` link list + inline `<style>` with trend cards (rank badge, hashtag, post count, relative last-used time) in a responsive grid; added `.filter-tabs`/`.filter-tab` + `.trend-card` CSS on design tokens; defined missing `--shadow-card-hover`. Also fixed a pre-existing bug found while verifying T4: community IDs are URLs containing `/`, which `[Route("{communityId}")]` could never match (every Show page 404'd) — moved `communityId` from the path to a query/form param (`/communities/show?communityId=`, Join/Leave/Delete via hidden form field)*
6. ✅ Admin: consistent dashboard layout, stat cards, table styling — *Reports + AuditLog now use the shared `.admin-page` wrapper + `.admin-nav` + `.admin-table` + `.empty-state` (were bare `.table`, no container/nav); Reports actions moved into `.action-cell`/`.inline-form`; admin CSS switched from hardcoded colors to design tokens (`--card-radius`, `--shadow-card`, `--color-accent`, `--color-border(-light)`, `--color-danger` mix for blocked rows); defined the previously-missing `.admin-card`; added dark-theme overrides for `.admin-table`/`.stat-card`*
7. ✅ Dark mode toggle (CSS custom properties, preference persisted in `localStorage`; token overrides under `[data-theme=dark]`) — *full dark palette + `[data-theme=dark]` overrides, toggle button + `localStorage` persistence in layout; mobile hamburger bars + hover states fixed*
 8. ✅ Accessibility: contrast audit, focus-visible styles, alt text on images, form labels — *global `:focus-visible` outline rule (WCAG 2.4.7); note-card overflow trigger now has `aria-label` (plus `title`); placeholder-only inputs (MRF domain/word, Suggestions filter, FederationHealth domain) given `aria-label`; verified all 10 `<img>` have `alt` and theme-toggle/hamburger already carry `aria-label`+`aria-controls`; darkened `--color-text-muted` (#888→#6c6c6c) and `--color-text-faint` (#999→#606060) and `--dark-text-muted` (#80809a→#9a9ab5) to meet WCAG AA 4.5:1; added 6 `AccessibilityTests` (focus-visible, light+dark contrast, layout a11y names, btn-more name, img-alt sweep) — suite now 803/803*
 9. ✅ Footer: useful links (about, help, server stats) instead of single tagline — *created a public `/about` page (Home/About + `Views/Home/About.cshtml`) with real content (what Fediblog is, capabilities, help, tech stack); footer now has a labeled `<nav aria-label="Footer">` with useful links (About, Trending, Communities, Home, and Server health when authenticated) on top of the tagline; footer text re-colored for WCAG AA contrast on the dark bar (#b9b9d6 tagline / #d0d0ea links). Note: the default route's `{controller=Home}` default only applies at `/`, so About needed an explicit `[Route("about")]`*
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

1. ✅ Local REST API: `/api/v1/statuses`, `/api/v1/accounts`, `/api/v1/timelines` (Mastodon-compatible DTOs, numeric status IDs, cookie-session auth)
2. ✅ Application registration flow (ClientID/ClientSecret): `POST /api/v1/apps` issues a client_id + one-time client_secret; `GET /api/v1/apps` lists the caller's apps (secret omitted). Backed by new `OAuthClientEntity` + `IApplicationRepository` (EF + InMemory). 5 API tests.
3. ✅ OAuth 2.0 PKCE for API authentication: `GET /api/v1/oauth/authorize` (cookie-auth, issues single-use code, 302 redirect) + `POST /api/v1/oauth/token` (authorization_code + PKCE S256/plain, returns Bearer access_token). Username-keyed `OAuthCodeEntity`/`OAuthTokenEntity` + `IApplicationRepository` methods (EF + InMemory). `BearerToken` auth scheme (`BearerTokenAuthenticationHandler`) so API controllers accept cookie **or** Bearer. 7 API tests.
4. ✅ API rate limiting with configurable limits per application: `ApiRateLimiter` (per-client fixed-window) + `ApiRateLimitingMiddleware` on `/api/v1/*`. Bucket key = OAuth `client_id` (Bearer) or username (cookie), else IP. Mastodon-style `RateLimit-Limit/-Remaining/-Reset` headers; 429 on exceed. Configurable via `ApiRateLimit` config section + `PerApplication` client_id overrides. 10 tests (unit + web).
5. ✅ API documentation (Swagger/OpenAPI): Swashbuckle Swagger UI at `/swagger` + OpenAPI 3.x JSON at `/swagger/v1/swagger.json`. Advertise `Bearer` (OAuth 2.0) + `Cookies` security schemes; include XML doc comments from controllers. XML doc generation enabled in WebUI csproj; doc comments added to all `/api/v1` action methods. 3 web tests.
6. ✅ Webhook support for external integrations: REST CRUD at `POST/GET/DELETE /api/v1/webhooks` (`ApiWebhooksController`, HMAC secret auto-generated). Wired `IWebhookDeliveryService` into `ComposeController.Post` so each new activity queues a `post.created` webhook delivery (try/catch — never blocks posting). Registered `AddWebhookServices()` + `WebhookDeliveryBackgroundService` in WebUI (10s poll, retries with backoff, max 5 attempts, HMAC-SHA256 signed payload). 8 API tests.

### Phase 38: Federation Hardening

**Goal:** Production-grade federation reliability. (Use the public host `https://openpub.luit.ink/` and @RayvenMX@mastodon.world for task 4.)

1. ✅ HTTP Signature verification for incoming activities: rewrote `HttpSignatureMiddleware` to verify W3C draft-cavage signatures over the raw signed-content bytes (`RSA-SHA256/PKCS#1`, `rsa.VerifyData`) instead of a pre-computed hash; normalized `(request-target)`/`(host)` component names; added replay protection (`created`/`expires`, 300s skew) and body-digest validation. Options-driven posture via `ActivityPubOptions.EnableSignatureVerification` + `RequireSignatures` (tolerates unsigned by default for local dev; `RequireSignatures=true` for full production). Fixed `OutboundSigningService` to keep its `headers` param and signed content in agreement (no double `digest`, `Date`/`Digest` set before signing). Fixed `KeyFetchingService` to fetch the keyId's base URL (actor JSON-LD doc). Wired the middleware into WebUI `Program.cs` after `UseAuthorization`. 16 new/updated tests (10 in `HttpSignatureVerificationTests` + 6 rewritten integration/middleware tests).
2. ✅ Delivery retry with exponential backoff: added `DeliveryRetryOptions` to `ActivityPubOptions` (`MaxRetries`, `BaseRetryDelaySeconds`, `UseExponentialBackoff`, `MaxRetryDelaySeconds` cap). Added `NextRetryAt` backoff-gate column to `SharedInboxDeliveryEntity`. Made `GetPendingSharedInboxDeliveriesAsync` time-aware (a `Failed` row is only re-eligible once `NextRetryAt` has passed and `RetryCount < maxRetries`; max retries is now a parameter, no longer hardcoded to 3). Rewrote `SharedInboxService.ProcessQueueAsync` to use a config-driven `HandleDeliveryFailure` helper that increments the retry count, sets an exponential `NextRetryAt` (`base * 2^(n-1)`, capped), and moves items to the terminal `MaxRetriesExceeded` dead-letter state. **Fixed a latent bug:** previously failed items were fetched for retry but never transitioned back to `Processing`, so retries never actually re-attempted; now `Queued`/`Failed` both become `Processing`. 7 new tests in `SharedInboxDeliveryRetryTests`.
3. ✅ Federation peer health tracking (auto-block unreliable servers): new `FederationPeerEntity` (keyed by domain) + `FederationPeers` DbSet tracks per-remote-server reliability (`ConsecutiveFailures`, `ConsecutiveSuccesses`, `TotalDeliveries`, `TotalFailures`, `ConsecutiveProbeFailures`, `IsBlocked`, `BlockedAt`, `BlockedReason`, liveness-probe fields). New `PeerHealthOptions` on `ActivityPubOptions` (`Enabled`, `AutoBlockThreshold`=5, `AutoUnblockSuccessThreshold`=3, `AutoBlockProbeFailureThreshold`=3, `ProbeIntervalMinutes`=5). New `IPeerHealthService`/`PeerHealthService` records delivery + probe outcomes and auto-blocks a peer at the failure threshold, auto-re-admits it after enough consecutive successes, and auto-blocks on sustained probe unreachability; also exposes manual `BlockDomainAsync`/`UnblockDomainAsync` + `IsDomainBlockedAsync`. Wired into `SharedInboxService`: `ProcessQueueAsync` skips (and backoff-requeues, without contacting the sender or recording a failure) deliveries to blocked peers and records delivery outcomes for peer health; `ProcessAndDistributeActivityAsync` rejects inbound activities whose origin domain is blocked. New `PeerHealthBackgroundService` periodically probes known peers via WebFinger and records reachability. Repository methods added to the interface + EF + in-memory impls. 14 new tests (`PeerHealthServiceTests` + `PeerHealthDeliveryIntegrationTests`).
4. ✅ Server-to-server federation testing with remote ActivityPub servers: fixed the critical bugs that prevented real outbound federation. (a) **Critical bug:** `SharedInboxService.ProcessQueueAsync` called `SendActivityAsync` with `string.Empty` as the private key, so every delivery threw `ArgumentNullException` and dead-lettered; now it retrieves the sender's private key from the local actor's `AdditionalProperties["privateKeyPem"]` via a new `GetPrivateKeyPemAsync`/`ExtractUsernameFromActorId` helpers, and gracefully fails with a clear reason when no key is available. (b) Added the required W3C `created` signature parameter to `OutboundSigningService` (Mastodon and most servers expect it). (c) Removed the `BuildInboxUrl` HEAD-probing loop (sync-over-async `.Result` deadlock risk + treated 404 as valid); now uses the stable `endpoint/inbox` path. (d) Fixed `WebFingerController.GetActivityPubEndpoint` to include the scheme+host in the `self` href (was `localhost/users/x`, now `https://host/users/x`) so remote servers can resolve our actors. (e) Fixed inconsistent key ID (`/#main-key` → `#main-key`) in `ActorController.GetActor`. Added `appsettings.json` to WebUI so `Domain`/`EnableFederation` are configurable. 4 new end-to-end tests in `OutboundFederationEndToEndTests` verify the sign→verify round-trip (outbound signer produces a valid signature accepted by our inbound `HttpSignatureMiddleware`), the `created` parameter, private-key retrieval from the actor record, and graceful failure when no key exists.
5. ✅ Inbox processing error handling and dead letter queue: new `InboxDeadLetterEntity` (keyed by `Id`, stores `ActivityId`, `RawJson`, `Username`, `Status`, `AttemptCount`, `FailureReason`, `LastAttemptAt`, timestamps) + `InboxDeadLetterStatus` enum (`DeadLettered`, `Processing`, `Failed`, `Replayed`). New `InboxProcessingOptions` on `ActivityPubOptions` (`Enabled`, `MaxAttempts`=3, `BaseRetryDelaySeconds`, `UseExponentialBackoff`, `MaxRetryDelaySeconds` cap). Rewrote `SharedInboxService.ProcessAndDistributeActivityAsync(username, activity, rawJson)` with a retry loop: validates activity, rejects blocked peers, then retries `ProcessAndDistributeCoreAsync` up to `MaxAttempts` with exponential backoff; on exhaustion calls `HandleInboxDeadLetterAsync` to persist the raw payload to the DLQ and returns `true` so the remote server stops redelivering. Added `ProcessInboxDeadLettersAsync(batchSize)` to replay DLQ items (re-process, mark `Replayed` on success / `Failed` on failure). New `InboxDeadLetterBackgroundService` periodically reprocesses + prunes stale DLQ items. `ActorController.PostInbox` now captures the exact raw request body (resetting `Request.Body.Position` after `[FromBody]` model binding consumed it) and passes it to the service for DLQ replay; returns 400 for client-side rejections (missing fields, unsupported type, blocked peer) and 500 for server-side failures. Repository methods (`AddInboxDeadLetterAsync`, `GetInboxDeadLetterAsync`, `GetReprocessableInboxDeadLettersAsync`, `UpdateInboxDeadLetterAsync`, `GetInboxDeadLetterCountAsync`) added to the interface + EF + in-memory impls. `FederationDeadLetters` DbSet registered in `ActivityPubDbContext.OnModelCreating`. Admin dashboard shows a `DeadLetterCount` card. 18 new `InboxDeadLetterTests` + 5 controller unit tests (including raw-body capture verification).

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
