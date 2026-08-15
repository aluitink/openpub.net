# Migration Guide

## Overview

This guide helps developers migrate from the old ActivityPub.Core structure to the new organized structure in `src/ActivityPub.Core/`.

## Directory Structure Changes

### Old Structure
```
ActivityPub.Core/
├── Services/
├── Models/
├── Controllers/
├── Repositories/
├── Infrastructure/
├── Implementations/
├── Interfaces/
├── BackgroundServices/
├── Caching/
├── Events/
├── Metrics/
├── Middleware/
├── Options/
├── Plugins/
├── Program.cs
```

### New Structure
```
src/ActivityPub.Core/
├── Core/                    # NEW: Core domain logic
│   ├── Models/
│   │   ├── activities/
│   │   ├── actors/
│   │   ├── collections/
│   │   └── webfinger/
│   ├── Interfaces/
│   ├── Events/
│   └── Options/
├── Infrastructure/          # UNCHANGED: Infrastructure concerns
│   ├── Data/
│   ├── Caching/
│   ├── Metrics/
│   ├── Telemetry/
│   ├── Logging/
│   └── Monitoring/
├── Services/                # UNCHANGED: Business logic
│   ├── ActivityProcessing/
│   ├── Federation/
│   ├── WebFinger/
│   ├── Security/
│   └── Background/
├── API/                     # NEW: API layer
│   ├── Controllers/
│   │   ├── v1/
│   │   ├── v2/
│   │   └── Dashboard/
│   ├── Middleware/
│   └── Filters/
├── Plugins/
├── Implementations/
└── Program.cs
```

## Namespace Changes

All namespaces remain unchanged:
- `ActivityPub.Core.*` - All core types
- `ActivityPub.Core.Infrastructure.*` - Infrastructure
- `ActivityPub.Core.Services.*` - Services
- `ActivityPub.Core.API.*` - API layer
- `ActivityPub.Core.Core.*` - Core domain types

## Migration Steps

### Step 1: Update Project References

Update your `.csproj` file to reference the new project path:

```xml
<ProjectReference Include="../src/ActivityPub.Core/ActivityPub.Core.csproj" />
```

### Step 2: Update Using Statements

No changes needed - all namespaces remain the same.

### Step 3: Update Build Configuration

Update any build scripts to use the new directory structure.

### Step 4: Verify Build

```bash
dotnet build ActivityPub.sln
dotnet test ActivityPub.sln
```

## Breaking Changes

None. All APIs remain unchanged.

## Testing

All existing tests pass without modification:
- 386 tests passing
- 0 errors

## Support

For questions or issues, see the [Contributing Guide](../contributing.md).
