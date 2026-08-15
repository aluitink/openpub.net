# Test Quality Improvement Plan - Phase 2

## Executive Summary
Phase 1 completed: 197 tests removed (402 → 205), test runtime reduced by ~50%.
Phase 2 focus: Additional cleanup, improving test coverage, and performance optimization.

## Current State (Post-Phase 1)
- **Total tests:** 205
- **Test runtime:** ~675ms
- **Build status:** ✅ Passing
- **Vulnerabilities:** 1 high severity (SQLitePCLRaw)

## Next Work Items

### 1. LoadTesting/LoadTestBase.cs Review
**File:** `LoadTesting/LoadTestBase.cs`
**Issue:** Utility methods still used by tests, but some may be redundant
**Impact:** Maintenance burden vs. utility value
**Action:** Review and remove unused methods

### 2. Test Coverage Analysis
**Issue:** No clear indication of what functionality is tested
**Action:** 
- Add coverage reporting (coverlet)
- Identify untested critical paths
- Add integration tests for missing scenarios

### 3. Dependency Injection Tests
**Issue:** Service collection configuration not explicitly tested
**Action:** Add tests for DI container setup

### 4. Error Handling Tests
**Issue:** Limited coverage of error scenarios
**Action:** Add tests for exception handling, validation errors

### 5. API Controller Tests
**Issue:** Controllers tested only for happy paths
**Action:** Add tests for error responses, validation, authentication

## Phase 2 Priorities

### High Priority
1. **Add:** Test coverage reporting (coverlet)
2. **Add:** Error handling tests for critical services
3. **Review:** LoadTesting/LoadTestBase.cs utility methods

### Medium Priority
4. **Add:** DI container configuration tests
5. **Add:** API controller error response tests
6. **Review:** Federation integration test coverage

### Low Priority
7. **Add:** Performance regression tests (using BenchmarkDotNet)
8. **Add:** Security vulnerability tests
9. **Add:** Concurrency tests for cache service

## Implementation Steps (Phase 2)
1. Add test coverage reporting
2. Identify and add error handling tests
3. Review and cleanup LoadTesting utilities
4. Add DI container tests
5. Add API controller tests
6. Document test strategy and coverage goals

## Notes
- Keep tests that validate business logic
- Remove tests that validate framework behavior
- Add integration tests for real-world scenarios
- Use BenchmarkDotNet for performance tests (not unit tests)
