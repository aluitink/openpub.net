# ActivityPub.NET - Project Plan

**Last Updated:** Aug 16, 2026
**Status:** Phases 1-35 complete. All core features implemented. 720/720 tests passing.

## Testing Guidelines

- **Browser/UI tests using Playwright** should be executed in delegated agents rather than inline, to isolate browser state and avoid conflicts with the main session.

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
| 35 | Content Discovery & Communities |  |  |

## Build State

- **Build:** 0 errors
- **Tests:** 720 passing, 0 failures
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
