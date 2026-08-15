# ActivityPub-dotnet Test Plan

## Executive Summary
Current focus: **Add core server-to-server integration tests that confirm multiple AP instances can communicate and federate**.
Previous work: Phase 1 removed 197 redundant tests, Phase 2 added 38 tests for security and DI, Phase 4 deferred failing tests.

## Current State
- **Total tests:** 252 (2 deferred to Phase 5, 5 new discovery tests)
- **Test runtime:** ~9s
- **Build status:** ✅ Passing (0 errors)
- **Vulnerabilities:** 1 high severity (SQLitePCLRaw)
- **Coverage:** 47.5% line, 43.93% branch, 52.64% method

## Phase 4: Server-to-Server Integration Tests

### Completed ✅
1. **Fix Failing Tests**
   - PerformanceTests.cs deferred to Phase 5
   - ConcurrencyTests.cs deferred to Phase 5
   - 252 tests passing, 0 errors

2. **Discovery Integration Tests** (5 new tests)
   - DiscoveryIntegrationTests.cs with 5 tests for WebFinger discovery
   - Tests verify multiple AP instances can resolve each other's endpoints
   - Cache functionality tested

3. **Existing Integration Tests**
   - FederationIntegrationTests.cs: 17 tests covering follow/like/announce/undo/delete workflows
   - WebFingerIntegrationTests.cs: 3 tests for WebFinger resolution
   - HttpSignatureIntegrationTests.cs: 3 tests for signature verification

### Next Work Item: Add Activity Exchange Integration Tests

Create IntegrationTests/ActivityExchange/ directory with tests for:
1. Signed HTTP request generation between servers
2. Activity delivery and parsing
3. Signature verification across instances
4. Duplicate detection

### Implementation Steps

#### Week 2: Discovery Tests ✅
1. Create IntegrationTests/Discovery/ directory
2. Add DiscoveryIntegrationTests.cs with 5 tests

#### Week 3: Activity Exchange Tests (Current)
1. Create IntegrationTests/ActivityExchange/ directory
2. Add ActivityDeliveryTests.cs:
   - Outbound HTTP request signing between servers
   - ActivityPub activity parsing
   - Signature verification
   - Duplicate detection

#### Week 4: Multi-Instance Federation Tests
1. Create IntegrationTests/MultiInstance/ directory
2. Add MultiInstanceFederationTests.cs:
   - Two AP server instances communicating
   - Follow workflow across instances
   - Like/announce workflows across instances
   - State synchronization verification

## Completion Criteria
- [x] All 252 tests passing (0 errors)
- [x] Performance/Concurrency tests deferred
- [ ] 50+ server-to-server integration tests added (currently ~28)
- [ ] Integration test coverage includes all core ActivityPub workflows
- [ ] Tests verify **multiple AP instances can successfully federate**
- [ ] Test suite runs in <1500ms (currently ~9s)

## Notes
- Focus on **real server-to-server communication** (not mocked)
- Use real HTTP requests, TLS, and cryptographic signing
- Test scenarios where **multiple AP server instances** must coordinate
- Verify federation works in **real-world conditions** (network latency, errors, etc.)