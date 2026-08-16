# ActivityPub.NET - Project Improvement Plan

**Last Updated:** Aug 15, 2026
**Status:** Phases 1-19 complete. Next phases planned below.

---

## Completed Phases (1-16)

| Phase | Title | Status |
|-------|-------|--------|
| 1 | Foundation - Directory structure | ✅ Complete |
| 2 | Documentation - README, guides, templates | ✅ Complete |
| 3 | Tooling - Build scripts, CI/CD, code quality | ✅ Complete |
| 4 | Cleanup - Empty files, solution consolidation | ✅ Complete |
| 5 | Source Migration - All files in new structure | ✅ Complete |
| 6 | Project Structure Validation | ✅ Complete |
| 7 | Full Test Suite with New Structure | ✅ Complete |
| 8 | Migration Verification | ✅ Complete |
| 9 | Documentation Updates - API reference, migration guide | ✅ Complete |
| 10 | Production Readiness - Vulnerabilities, packages | ✅ Complete |
| 11 | Identity Model - JWT support for DemoApp | ✅ Complete |
| 12 | Code Quality - Nullable warnings, package cleanup | ✅ Complete |
| 13 | Performance Optimization - Caching, batch processing | ✅ Complete |
| 14 | Security Hardening - Headers, validation, middleware | ✅ Complete |
| 15 | Deployment Documentation - Docker, K8s, scripts | ✅ Complete |
| 16 | Final Cleanup - Stale files, nullable warnings, .gitignore | ✅ Complete |
| 17 | CI/CD Pipeline Setup - GitHub Actions, CODEOWNERS | ✅ Complete |
| 18 | Admin Dashboard Scaffolding - ActivityPub.Admin | ✅ Complete |
| 19 | CLI Tool Scaffolding - ActivityPub.Cli | ✅ Complete |
| 20 | Integration Test Expansion - 502 tests | ✅ Complete |

---

## Current Build State

- **Build:** 0 errors, 0 warnings
- **Tests:** 502 passing, 0 failed
- **Format:** Clean (dotnet format --verify-no-changes passes)
- **Target Framework:** .NET 10.0
- **Branch:** qwen3.6-27b-eval

---

## Next Phases

### Phase 17: CI/CD Pipeline Setup ✅ Complete

**Goal:** Create proper GitHub Actions CI/CD workflow.

**Tasks:**
1. ✅ Create `.github/workflows/ci.yml` with build, test, and format check jobs
2. ✅ Create `.github/workflows/release.yml` for automated NuGet publishing
3. ✅ Add CODEOWNERS file
4. ✅ Run dotnet format to fix whitespace inconsistencies across codebase
5. ✅ Fix xUnit1031 and ASP0019 analyzer warnings

### Phase 18: Admin Dashboard Scaffolding ✅ Complete

**Goal:** Scaffold `ActivityPub.Admin` as a minimal admin dashboard project.

**Tasks:**
1. ✅ Create `src/ActivityPub.Admin/ActivityPub.Admin.csproj` (ASP.NET Core Razor Pages)
2. ✅ Add basic admin pages: Activity feed viewer, Actor management, Outbox monitoring
3. ✅ Add to solution file
4. ✅ Ensure it references ActivityPub.Core
5. ✅ Add layout, error page, and CSS styling

### Phase 19: CLI Tool Scaffolding ✅ Complete

**Goal:** Scaffold `ActivityPub.Cli` as a command-line administration tool.

**Tasks:**
1. ✅ Create `src/ActivityPub.Cli/ActivityPub.Cli.csproj` (Console app with System.CommandLine)
2. ✅ Implement actor commands: list, get, follow, unfollow
3. ✅ Implement activity commands: list, get, fetch, delete
4. ✅ Implement inbox commands: monitor, pending, stats
5. ✅ Implement webhook commands: list, pending, history, delete
6. ✅ Add to solution file
7. ✅ Ensure it references ActivityPub.Core

### Phase 20: Integration Test Expansion ✅ Complete

**Goal:** Expand integration test coverage for federation scenarios.

**Tasks:**
1. ✅ Add tests for WebFinger protocol exchange (12 tests)
2. ✅ Add tests for OStatus HTTP Signature verification (25 tests)
3. ✅ Add tests for inbox delivery (29 tests)
4. ✅ Add tests for ActivityPub object type validation (20 tests)
5. ✅ Target: 500+ total tests (achieved: 502)

### Phase 21: Benchmark Suite

**Goal:** Add performance benchmarks using BenchmarkDotNet.

**Tasks:**
1. Create `ActivityPub.Benchmarks/` project
2. Benchmark JSON serialization/deserialization for ActivityPub objects
3. Benchmark cache lookup performance (MemoryFederationCache)
4. Benchmark HTTP signature generation/verification
5. Add baseline metrics to documentation

### Phase 22: Observatory Compliance

**Goal:** Implement ActivityPub compliance checks.

**Tasks:**
1. Implement WebFinger endpoint per Mastodon Observatory spec
2. Implement nodeinfo endpoint (2.0 and 2.1)
3. Implement well-known host metadata
4. Add compliance test suite

---

## Project Structure (Current)

```
/workspace/
├── ActivityPub.sln
├── src/
│   ├── ActivityPub.Core/        # Core library (Controllers, Services, Models, Infrastructure)
│   ├── ActivityPub.Admin/       # Admin dashboard (Razor Pages)
│   └── ActivityPub.Cli/         # CLI administration tool (System.CommandLine)
├── ActivityPub.Tests/           # Test suite (386 tests)
│   ├── UnitTests/
│   ├── IntegrationTests/        # Includes Scale/ subfolder
│   ├── LoadTesting/
│   └── Deferred/
├── samples/
│   ├── ConsoleClient/
│   ├── FederationApp/
│   ├── BotApp/
│   └── DemoApp/
├── docs/
│   ├── overview.md
│   ├── architecture.md
│   ├── testing.md
│   ├── directory-structure.md
│   ├── migration-guide.md
│   ├── contributing.md
│   └── api-reference/
├── scripts/
│   ├── build.sh
│   ├── test.sh
│   ├── publish.sh
│   ├── deploy.sh
│   ├── backup.sh
│   └── restore.sh
├── DOCKER.md
├── ENVIRONMENT.md
└── kubernetes.yaml
```
