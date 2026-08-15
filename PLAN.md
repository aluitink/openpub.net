# ActivityPub-dotnet Integration Test Plan

## Executive Summary

**Current Focus:** Build comprehensive server-to-server integration tests that confirm multiple ActivityPub instances can successfully federate.

**Status:** 275 tests passing, 0 errors. 49 integration tests complete.

### Phase 3 Completion Summary
- ✅ Activity Exchange Integration Tests: 15 tests (ActivityDeliveryTests, SignatureVerificationTests, DuplicateDetectionTests)
- ✅ Multi-Instance Federation Tests: 8 tests (MultiInstanceFederationTests)
- ✅ All tests passing with proper error handling

---

## Current Test Inventory

### Integration Test Files (Phase 3+)

| Test File | Tests | Status |
|-----------|-------|--------|
| FederationIntegrationTests.cs | 17 | ✅ Complete |
| WebFingerIntegrationTests.cs | 3 | ✅ Complete |
| HttpSignatureIntegrationTests.cs | 3 | ✅ Complete |
| DiscoveryIntegrationTests.cs | 5 | ✅ Complete |
| ActivityExchange/ActivityDeliveryTests.cs | 5 | ✅ Phase 3 |
| ActivityExchange/SignatureVerificationTests.cs | 5 | ✅ Phase 3 |
| ActivityExchange/DuplicateDetectionTests.cs | 5 | ✅ Phase 3 |
| MultiInstance/MultiInstanceFederationTests.cs | 8 | ✅ Phase 3 |

---

## Phase 3: Integration Test Expansion (COMPLETE ✅)

### Results
- **Total Integration Tests:** 49
- **Phase 3 Tests Added:** 24 new tests
- **Status:** All tests passing (275 total)

### Completed Test Categories

#### 1. Activity Exchange Integration Tests (15 tests)
**Directory:** `IntegrationTests/ActivityExchange/`

✅ ActivityDeliveryTests.cs (5 tests)
- Valid activity posting and parsing
- ActivityPub activity parsing
- Activity validation
- Content-type header handling
- Activity propagation verification

✅ SignatureVerificationTests.cs (5 tests)
- Valid signature acceptance
- Malformed signature handling
- Missing signature handling
- Signature header preservation
- Multiple signature handling

✅ DuplicateDetectionTests.cs (5 tests)
- Same activity ID idempotency
- Different activities acceptance
- Same content different ID acceptance
- Inbox activity reception
- Infinite loop prevention

#### 2. Multi-Instance Federation Tests (8 tests)
**Directory:** `IntegrationTests/MultiInstance/`

✅ MultiInstanceFederationTests.cs (8 tests)
- Follow workflow completion
- Like workflow completion
- Announce workflow completion
- Undo workflow completion
- Delete workflow completion
- Multiple workflow execution
- Concurrent federation operations
- State consistency verification

---

## Phase 4: Performance & Parallelization (IN PROGRESS)

### Targets
- **Integration Tests:** 75+ tests
- **Total Tests:** 400+ tests
- **Runtime:** <15s with parallelization
- **Load Testing:** Basic infrastructure

### Tasks
1. **Parallelization** - Configure test runner for parallel execution
2. **Performance Baseline** - Establish baseline metrics for regression detection
3. **Load Testing** - Add basic load testing infrastructure
4. **Integration Test Expansion** - Add 26+ additional integration tests

---

## Implementation Schedule (Phase 3 - COMPLETE)

### Completed: Activity Exchange Tests
- ✅ Created `IntegrationTests/ActivityExchange/` directory
- ✅ Added ActivityDeliveryTests.cs (5 tests)
- ✅ Added SignatureVerificationTests.cs (5 tests)
- ✅ Added DuplicateDetectionTests.cs (5 tests)
- **Result:** 15 activity exchange tests

### Completed: Multi-Instance Federation Tests
- ✅ Created `IntegrationTests/MultiInstance/` directory
- ✅ Added MultiInstanceFederationTests.cs (8 tests)
- **Result:** 8 multi-instance tests

### Completed: Dependency Injection Tests
- ✅ Added DependencyInjectionTests.cs (5 tests)
- **Result:** 5 DI container tests

### Completed: Security Tests
- ✅ Added security vulnerability tests (43 tests)
- **Result:** All security tests passing

---

## Completion Criteria

### Phase 3 (COMPLETE ✅)
- ✅ **49 integration tests** implemented
- ✅ **275 total tests** passing
- ✅ **0 build errors**
- ✅ Tests verify **multiple AP instances can successfully federate**

### Phase 4 (IN PROGRESS)
- ⏳ **75+ integration tests**
- ⏳ **400+ total tests**
- ⏳ **<15s runtime** with parallelization
- ⏳ **Load testing infrastructure** added

### Phase 5 (PLANNED)
- ⏳ **100+ integration tests**
- ⏳ **500+ total tests**
- ⏳ **Full load testing** with realistic workloads
- ⏳ **Parallel execution** across all test categories

---

## Notes

- Focus on **real server-to-server communication** (not mocked)
- Use real HTTP requests, TLS, and cryptographic signing
- Test scenarios where **multiple AP server instances** must coordinate
- Verify federation works in **real-world conditions** (network latency, errors, etc.)
- Each test should explicitly verify **inter-instance federation success**
