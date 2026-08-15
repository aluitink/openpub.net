# ActivityPub Core Services Architecture

## Overview

This document explains how the core services in ActivityPub .NET work together to process ActivityPub activities.

## Service Layers

### 1. API Layer (Controllers)

**Location**: `ActivityPub/Controllers/`

Handles HTTP requests and returns responses. Controllers delegate to services.

Key controllers:
- `ActivityController`: Activity processing (Create, Update, Delete)
- `InboxController`: ActivityPub inbox operations
- `ActorController`: Actor/profile operations
- `FederationController`: Federated operations

### 2. Service Layer

**Location**: `ActivityPub/Services/`

Business logic layer. Services coordinate between API and repository.

Key services:
- **ActivityService**: Process incoming/outgoing activities
- **ActorService**: User actor management
- **FederationService**: Cross-server communication

### 3. Repository Layer

**Location**: `ActivityPub/Core/Repositories/`

Data access layer using EF Core.

Key repositories:
- **EFCoreActivityPubRepository**: Database operations
- **CacheRepository**: Redis/Memcached caching
- **WebFingerRepository**: User discovery

## Activity Flow

### Incoming Activity (Inbox)

```
1. HTTP POST /users/{username}/inbox
   ↓
2. InboxController.ReceiveActivity()
   - Validate JSON format
   - Verify signature (if enabled)
   ↓
3. ActivityService.ProcessActivity(activity)
   - Parse activity type (Create, Like, Announce)
   - Extract object (Note, Article, etc.)
   - Validate actor permissions
   ↓
4. ActivityPubRepository.SaveActivityAsync(activity)
   - Create ActivityEntity
   - Save to database
   ↓
5. Return 202 Accepted (async processing)
```

### Outgoing Activity (Outbox)

```
1. Actor creates activity
   ↓
2. ActivityService.SaveToOutbox(actor, activity)
   ↓
3. ActivityPubRepository.SaveActivityAsync(activity)
   ↓
4. ActivityPubRepository.AddActivityToOutbox(actor, activityId)
   ↓
5. Return activity ID
```

## Database Schema

### Tables

| Table | Purpose |
|-------|---------|
| `Actors` | User actors (Person, Organization) |
| `Activities` | All ActivityPub activities |
| `WebFingerCache` | Cached webfinger responses |
| `FederationLog` | Federation request history |

### Key Entities

```csharp
public class ActorEntity
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string JsonData { get; set; }  // Full actor JSON
    public DateTime CreatedAt { get; set; }
}

public class ActivityEntity
{
    public int Id { get; set; }
    public required string ActivityId { get; set; }  // Activity URI
    public required string JsonData { get; set; }    // Full activity JSON
    public DateTime CreatedAt { get; set; }
}
```

**Note**: Full JSON stored in `JsonData` for flexibility and ActivityPub compatibility.

## Background Services

### SharedInboxBackgroundService

**Location**: `ActivityPub/Services/Background/`

Processes shared inbox activities asynchronously.

**Workflow**:
1. Timer triggers every 5 seconds
2. Fetch pending activities from shared inbox
3. Process each activity
4. Update status

**Configuration**:
```csharp
services.AddHostedService<SharedInboxBackgroundService>();
```

**Disable for tests**:
```csharp
builder.Services.RemoveAll(typeof(IHostedService));
```

## Repository Pattern

### IActivityPubRepository Interface

```csharp
public interface IActivityPubRepository
{
    // Actor operations
    Task<Actor?> GetUserActorAsync(string username);
    Task SaveUserActorAsync(Actor actor);
    
    // Activity operations
    Task<Activity?> GetActivityAsync(string activityId);
    Task SaveActivityAsync(Activity activity);
    
    // Inbox operations
    Task<IEnumerable<Activity>> GetInboxActivitiesAsync(string username);
    
    // Outbox operations
    Task AddActivityToOutboxAsync(string username, string activityId);
}
```

### Implementation: EFCoreActivityPubRepository

Uses EF Core for database operations:

```csharp
public class EFCoreActivityPubRepository : IActivityPubRepository
{
    private readonly ActivityPubDbContext _context;
    
    public async Task SaveActivityAsync(Activity activity)
    {
        var entity = new ActivityEntity
        {
            ActivityId = activity.Id!,
            JsonData = JsonSerializer.Serialize(activity)
        };
        
        _context.Activities.Add(entity);
        await _context.SaveChangesAsync();
    }
}
```

## Caching Strategy

### Two-Level Cache

1. **In-Memory (IMemoryCache)**: Fast access, app-scoped
2. **Distributed (IDistributedCache)**: Cross-server, Redis/Memcached

### Cache Keys

```
actor:{username}          → Actor profile
activity:{activityId}     → Activity data
inbox:{username}          → Inbox activities
webfinger:{username}      → Webfinger response
```

## Federation

### Outbound Federation

1. Activity saved to local database
2. ActivityPubRouter identifies remote followers
3. HTTP POST to remote server's inbox
4. Log result in FederationLog

### Inbound Federation

1. Remote server POSTs to local inbox
2. Signature verification (optional)
3. Activity processing
4. Response: 202 Accepted

## Configuration

### Services Registration

```csharp
builder.Services.AddDbContext<ActivityPubDbContext>(options =>
    options.UseSqlite("Data Source=activitypub.db"));

builder.Services.AddScoped<IActivityPubRepository, EFCoreActivityPubRepository>();
builder.Services.AddScoped<IActivityPubService, ActivityPubService>();
```

### Background Services

```csharp
builder.Services.AddHostedService<SharedInboxBackgroundService>();
```

## Key Design Principles

1. **JSON Storage**: Full ActivityPub JSON stored, not POCO properties
2. **Async All The Way**: All operations are async
3. **Repository Pattern**: Decouple data access from business logic
4. **Background Processing**: Inbox activities processed asynchronously
5. **Cache First**: Check cache before database queries

## Testing Strategy

### Unit Tests
- Test individual services/repositories in isolation
- Mock dependencies
- Fast execution (<5s)

### Integration Tests
- Test full flow through controllers
- Database operations
- Background services (optional)

### Scale Tests
- Large dataset processing
- Concurrent operations
- Performance verification

## Performance Considerations

1. **Batch Operations**: Process multiple activities together
2. **Caching**: Cache frequent queries (actors, activities)
3. **Background Processing**: Offload processing from HTTP requests
4. **Database Indexing**: Index on ActivityId and CreatedAt
5. **Connection Pooling**: Reuse database connections
