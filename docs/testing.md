# ActivityPub .NET Testing Guide

## Overview

This guide explains the testing structure and approach for the ActivityPub .NET implementation. The test suite is organized by testing phases, with Phase 5 (integration testing) complete and Phase 6 (scale/optimization testing) in progress.

## Test Phases

### Phase 1-4: Unit Testing
- **Status**: Complete
- **Location**: `ActivityPub.Tests/UnitTests/`
- **Coverage**: Core service logic, repositories, models
- **Test Count**: 150+ tests
- **Runtime**: <5s

### Phase 5: Integration Testing  
- **Status**: Complete
- **Location**: `ActivityPub.Tests/IntegrationTests/`
- **Coverage**: Database operations, API endpoints, background services
- **Test Count**: 93 integration tests, 353+ total tests
- **Runtime**: ~9s (parallelized)

### Phase 6: Scale Testing
- **Status**: In progress
- **Location**: `ActivityPub.Tests/IntegrationTests/Scale/`
- **Coverage**: Performance, concurrent operations, large datasets
- **Test Count**: 44 scale tests (15 new in this phase)
- **Runtime**: ~9s for all tests

## Test Organization

### Scale Tests (`IntegrationTests/Scale/`)

Each test file targets a specific area:

| File | Tests | Purpose |
|------|-------|---------|
| `ActivityHistoryTests.cs` | 5 | Activity storage/retrieval |
| `ActorScaleTests.cs` | 5 | Actor CRUD operations |
| `DatabaseScaleTests.cs` | 5 | Database bulk operations |
| `QueryScaleTests.cs` | 5 | Complex queries/pagination |
| `InboxScaleTests.cs` | 5 | Inbox processing at scale |
| `ActivityProcessingScaleTests.cs` | 5 | Activity processing |
| `OutboxScaleTests.cs` | 5 | Outbox operations |

### Test Fixture Pattern

```csharp
public class ScaleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ScaleTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }
}
```

**Purpose**: Single app instance shared across all tests in class, reducing startup overhead.

### Test Isolation

All scale tests use `Guid.NewGuid()` for unique identifiers:

```csharp
var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
var username = $"user-{testRunId}";
```

This prevents test pollution when tests run in parallel.

## Core Services Flow

### ActivityPub Service Architecture

```
User Request
    ↓
API Controller (ActivityController, InboxController, etc.)
    ↓
Service Layer (ActivityService, ActorService, etc.)
    ↓
Repository Layer (EF Core + Custom Repository)
    ↓
Database (SQLite for tests)
```

### Request Flow Example: Inbox Delivery

1. **InboxController.ReceiveActivity()** receives POST request
2. **ActivityService.ProcessActivity()** validates and processes
3. **ActivityPubRepository.SaveActivityAsync()** saves to database
4. **SharedInboxBackgroundService** (if enabled) processes asynchronously
5. Response returned to sender

### Background Services

- **SharedInboxBackgroundService**: Processes shared inbox activities
- **Disabled in tests**: Use `TestWebApplicationFactoryWithoutBackgroundServices` to disable

```csharp
// Disable background services for test isolation
builder.Services.RemoveAll(typeof(IHostedService));
```

## Writing New Tests

### Scale Test Template

```csharp
[Fact]
public async Task MyScaleTest_CanDoSomething()
{
    // 1. Generate unique test ID
    var testRunId = Guid.NewGuid().ToString("n").Substring(0, 8);
    
    // 2. Create test data
    using var scope = _factory.Services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IActivityPubRepository>();
    
    // 3. Perform operations
    var actor = new Actor { /* ... */ };
    await repository.SaveUserActorAsync(actor);
    
    // 4. Verify results
    using var dbScope = _factory.Services.CreateScope();
    var context = dbScope.ServiceProvider.GetRequiredService<ActivityPubDbContext>();
    var count = await context.Activities.CountAsync(a => a.ActivityId.Contains(testRunId));
    
    Assert.Equal(expected, count);
}
```

### Key Principles

1. **Always use unique IDs**: Prevents test interference
2. **Test at repository level**: More reliable than HTTP tests
3. **Use database context for verification**: Direct EF Core access
4. **Avoid background services**: Disable for predictable test results

## Running Tests

```bash
# Run all tests
dotnet test

# Run integration tests only
dotnet test --filter "FullyQualifiedName~Integration"

# Run scale tests only  
dotnet test --filter "FullyQualifiedName~Scale"

# Run specific test
dotnet test --filter "FullyQualifiedName~MyScaleTest"
```

## Test Results

Current status:
- **Total Tests**: 397
- **Integration Tests**: 93
- **Scale Tests**: 44
- **Build Errors**: 0
- **Test Runtime**: ~9s (parallelized)

## Troubleshooting

### Common Issues

1. **Test pollution**: Use unique `testRunId` with `Guid.NewGuid()`
2. **Background service interference**: Use `TestWebApplicationFactoryWithoutBackgroundServices`
3. **Database locks**: Close all `DbContext` instances in `using` blocks
4. **Race conditions**: Avoid parallel writes without proper transactions
