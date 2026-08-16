# ActivityPub.NET - Project Plan

**Last Updated:** Aug 16, 2026
**Status:** Phases 30-31 complete. Next: Phase 32 (Admin & Moderation).

## Testing Guidelines

- **Browser/UI tests using Playwright** should be executed in delegated agents rather than inline, to isolate browser state and avoid conflicts with the main session.

---

## Completed Phases (1-22)

| # | Title | # | | Title |
|---|-------|---|---|-------|
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

## Build State

- **Build:** 0 errors
- **Tests:** 630 passing (1 pre-existing failure: NodeInfo_Discovery_Returns_Versions)
- **Total Tests:** 631
- **Framework:** .NET 10.0
- **Branch:** qwen3.6-27b-eval

## Core Library Surface

**Models:** Actor, Note, Create, Follow, Like, Announce, Article, Page, Video, Image, Collection, OrderedCollection, Activity, Accept, Reject, Undo, Delete, Tombstone, Update, Event, PublicKey, Endpoints + WebFinger/NodeInfo/HostMeta discovery types

**Repository (`IActivityPubRepository`):** Actor CRUD, Activity CRUD, Outbox/Followers/Following/Liked collections, deduplication, shared inbox delivery queue, webhook delivery queue

**Services:** ActivityPubService (actor lookup, activity processing, cache invalidation), InboxProcessorService, OutboundActivityService, OutboundSigningService, FederationDiscoveryService, KeyFetching/Generation, SharedInboxService, WebhookDelivery, WebFingerCache, ActivityValidation, MRFService, ActivityCache, ActivityPubEventDispatcher

**Middleware:** RateLimiting, SecurityHeaders, HttpSignature, SigningVerification

**Infrastructure:** EFCoreActivityPubRepository (InMemory/SQLite), MemoryFederationCache, CacheInvalidation, Logging, Telemetry, Metrics, ResponseCaching, API Versioning, Monitoring

**DI Extension:** `AddActivityPub(Action<ActivityPubOptions>?)` — registers all services, DbContext, hosted services

**Discovery Endpoints:** WebFinger, NodeInfo 2.1, HostMeta, Health

---

## Social Platform: Fediblog

A Mastodon-like microblogging application built on ActivityPub.NET.

**Tech choices:** SQLite, username/password auth, minimal MVP (posting + federation)

**Project location:** `src/ActivityPub.WebUI/`

### Phase 25: WebUI Foundation & Auth

**Goal:** Scaffold the web application with user authentication and registration.

**Tasks:**
1. Create `src/ActivityPub.WebUI/ActivityPub.WebUI.csproj` (ASP.NET Core MVC, .NET 10)
2. Add to `ActivityPub.sln`, reference `ActivityPub.Core`
3. Configure SQLite via `Microsoft.EntityFrameworkCore.Sqlite`
4. Implement user registration page (username, password, display name, email)
5. Implement login/logout with cookie authentication
6. Create `ApplicationUser` entity and `ApplicationDbContext` (extends or alongside ActivityPubDbContext)
7. Seed local Actor on account creation (generate keypair, register in repository)
8. Basic layout: navbar with brand, login/register links, user menu
9. Add integration tests for auth flow

### Phase 26: Compose & Timeline

**Goal:** Users can create notes and see a home timeline.

**Tasks:**
1. Compose controller/action with form (text area, 500-char limit)
2. On submit: create Note, wrap in Create activity, save to outbox via `IActivityPubRepository`
3. Distribute Create activity to followers via `OutboundActivityService`
4. Home timeline: query `GetInboxActivitiesAsync` + local user's outbox, render chronological feed
5. Public timeline: show all local public notes
6. Note display card: author avatar/username, content, timestamp, like/boost/reply buttons
7. Delete note action (tombstone via `DeleteActivityAsync`)
8. Add integration tests for compose, timeline, delete

### Phase 27: Follows & Federation

**Goal:** Users can follow local and remote actors; remote content appears in timeline.

**Tasks:**
1. Follow form: accept username@domain, discover remote actor via `FederationDiscoveryService`
2. Send Follow activity via `OutboundActivityService`, handle Accept/Reject
3. Handle incoming Follow activities in inbox processor
4. Unfollow: send Undo(Follow) activity
5. Following/Followers pages: render lists from `GetFollowingAsync`/`GetFollowersAsync`
6. Remote notes appearing in timeline via inbox processing of Create activities
7. Add integration tests for follow/unfollow/federation flow

### Phase 28: Interactions (Like, Reply, Boost) — COMPLETE

**Goal:** Users can like, reply to, and boost (repost) notes.

**Tasks:**
1. ✅ Like: create Like activity, save to liked collection, distribute to author
2. ✅ Reply: create Note with `inReplyTo`, wrap in Create, distribute to conversation participants
3. ✅ Boost (Announce): create Announce activity referencing original, distribute
4. ✅ Reply threading in timeline view (show reply chains)
5. ✅ Interaction counts on note cards (likes, replies, boosts)
6. ✅ Add integration tests for each interaction type (13 tests)

### Phase 29: Profiles & Actor Endpoints

**Goal:** User profiles and federation-compatible actor endpoints.

**Tasks:**
1. ✅ Profile page: display name, bio, avatar, banner, stats (posts, followers, following)
2. ✅ Edit profile: update display name, summary, icon, image
3. ✅ Actor endpoint `/users/{username}` → ActivityPub Person JSON
4. ✅ Outbox endpoint `/users/{username}/outbox` → OrderedCollection
5. ✅ Followers/Following collection endpoints with pagination
6. ✅ Liked collection endpoint
7. ✅ Add integration tests for all actor endpoints (17 tests)

### Phase 30: Polish & Production — COMPLETE

**Goal:** Polish the UI, add missing UX, and prepare for deployment.

**Tasks:**
1. ✅ Responsive CSS (mobile-friendly)
2. ✅ Error pages (404, 500)
3. ✅ Rate limiting on compose and follow endpoints (path-scoped, 20 req/min, 3 tests)
4. ✅ Hashtag support: word-boundary matching, paginated feed (HashtagController, 5 tests)
5. ✅ Search: local user and note search (SearchController, 7 tests)
6. ✅ Notifications page: follows, mentions, likes (NotificationsController, 5 tests)
7. ✅ Dockerfile for ActivityPub.WebUI + docker-compose.yml
8. ✅ Deployment docs (docs/deployment.md)
9. ✅ Integration tests: 625 passing
10. ✅ README with Fediblog showcase

### Phase 31: Performance & Scalability — COMPLETE

**Goal:** Optimize data access, add caching, and improve response times for large-scale usage.

**Tasks:**
1. ✅ Database indexes: CreatedAt on ActivityEntity, composite (ActivityId, CreatedAt), unique ActorId on ApplicationUser
2. ✅ Response caching: TimelineController (3s), ProfileController (5s), ActorsController (5s), UseResponseCaching middleware
3. ✅ Repository JSON query optimization: Replaced full-table scans with SQL LIKE queries for outbox, inbox, likes, boosts, replies
4. ✅ New count methods: GetFollowerCountAsync, GetFollowingCountAsync on interface + both implementations
5. ✅ ProfileController: Uses count methods instead of loading all follow activities
6. ✅ ActorsController: OrderedCollectionPage with first/last/prev/next/partOf pagination links
7. ✅ Performance benchmarks: 5 integration tests (timeline/profile/hashtag speed, outbox format, follow scaling)
8. ⬜ Redis-backed distributed cache (future)
9. ⬜ Memory profiling and leak detection (future)
10. ⬜ Load testing with 100+ concurrent users (future)

### Phase 32: Admin & Moderation

**Goal:** Provide tools for site administrators and content moderation.

**Tasks:**
1. Admin dashboard: user management, activity overview, server stats
2. User roles: admin flag on ApplicationUser
3. Content moderation: block/mute users, delete foreign content from local DB
4. MRF (Moderation Rules Framework) integration: domain blocklist, keyword filters
5. ActivityPub moderation extensions: Block, Reject, Undo activities
6. Audit log: track admin actions
7. Report system: users can flag inappropriate content
8. Rate limit configuration from admin panel
9. Federation health monitoring: delivery queue status, error rates

### Phase 33: Extended Federation

**Goal:** Improve federation compatibility and add missing ActivityPub features.

**Tasks:**
1. Inbox processor: handle all ActivityPub activity types (Create, Delete, Update, Move)
2. Outbox: proper OrderedCollectionPage with pagination (first/next/prev/last)
3. Followers/Following: full OrderedCollectionPage endpoints
4. Article support: long-form content (extended description, HTML body)
5. Image uploads: attachment support with local hosting
6. Poll support in notes
7. Editable notes: Update activity for existing notes
8. Block/Undo block: ActivityPub Block activity support
9. Server-to-server federation testing with remote ActivityPub servers
