# Project Reorganization Plan

## Current State Analysis

### Issues Identified
1. **Unclear Folder Structure** - Services, Models, Repositories scattered without clear boundaries
2. **Mixed Concerns** - Infrastructure, Metrics, Telemetry mixed with core logic
3. **Redundant Files** - Multiple `Class1.cs`, empty files, duplicate naming
4. **Documentation Gaps** - Missing high-level overview, contribution guide
5. **Test Organization** - Integration tests not clearly categorized by feature
6. **Sample Projects** - Inconsistent structure, overlapping functionality

### Current Structure
```
/workspace/
├── ActivityPub.Core/              # Core library (45 files)
│   ├── Services/                  # 14 service files
│   ├── Models/                    # 27 model files
│   ├── Controllers/               # 5 controller files
│   ├── Repositories/              # 8 entity files
│   ├── Infrastructure/            # 6 subdirectories (Dashboard, Logging, Metrics, etc.)
│   ├── Implementations/           # 4 implementation files
│   ├── Interfaces/                # 3 interface files
│   ├── BackgroundServices/        # 1 file
│   ├── Caching/                   # 3 files
│   ├── Events/                    # 4 files
│   ├── Metrics/                   # 1 file
│   ├── Middleware/                # 3 files
│   ├── Options/                   # 1 file
│   ├── Plugins/                   # 3 files
│   ├── Class1.cs                  # Empty placeholder
│   ├── RejectActivityHandler.cs   # Empty file
│   └── obj/                       # Build artifacts
├── ActivityPub.Tests/             # Tests (95 files)
│   ├── IntegrationTests/          # 15 test files in subdirectories
│   ├── UnitTests/                 # Missing (tests directly in root)
│   ├── LoadTesting/               # 1 file
│   ├── Deferred/                  # 1 file
│   └── TestResults/               # Test output
└── SampleProjects/                # 4 sample apps
    ├── FederationApp/
    ├── BotApp/
    ├── DemoApp/
    └── ConsoleClient/
```

---

## Proposed Structure

### Root Directory
```
/workspace/
├── docs/                          # NEW: All documentation
│   ├── overview.md                # NEW: Project overview
│   ├── architecture.md            # Renamed from ARCHITECTURE_GUIDE.md
│   ├── testing.md                 # Renamed from TESTING_GUIDE.md
│   ├── state-report.md            # Renamed from ACTIVITYPUB_STATE_REPORT.md
│   ├── api-reference/             # NEW: API docs
│   ├── contributing.md            # NEW: Contribution guide
│   └── faq.md                     # NEW: FAQ
├── src/                           # NEW: Source code organized by feature
│   ├── ActivityPub.Core/          # Core library
│   ├── ActivityPub.Cli/           # NEW: Command-line tool
│   └── ActivityPub.Admin/         # NEW: Admin dashboard
├── tests/                         # NEW: All tests
│   ├── ActivityPub.Tests/
│   │   ├── UnitTests/
│   │   ├── IntegrationTests/
│   │   └── ScaleTests/            # Renamed from IntegrationTests/Scale
│   └── SampleProjects/
│       ├── FederationApp/
│       ├── BotApp/
│       ├── DemoApp/
│       └── ConsoleClient/
├── samples/                       # NEW: Standalone examples
│   ├── quickstart/                # NEW: Minimal working example
│   └── advanced/                  # NEW: Complex examples
├── scripts/                       # NEW: Build and deployment scripts
│   ├── build.sh                   # NEW: Cross-platform build
│   ├── test.sh                    # NEW: Test runner
│   ├── publish.sh                 # NEW: Publish script
│   └── ci/                        # NEW: CI/CD configs
├── .github/                       # NEW: GitHub workflows
│   ├── workflows/                 # NEW: GitHub Actions
│   └── ISSUE_TEMPLATE/            # NEW: Issue templates
├── .opencode/                     # Existing: AI assistant config
├── .gitignore
├── .dockerignore
├── ActivityPub.sln                # Consolidated solution file
├── README.md                      # NEW: Comprehensive overview
├── LICENSE                        # NEW: License file
├── SECURITY.md                    # NEW: Security policy
└── CODE_OF_CONDUCT.md             # NEW: Code of conduct
```

---

## Detailed Breakdown

### 1. Source Code Structure (`src/`)

#### ActivityPub.Core
```
src/ActivityPub.Core/
├── Core/                          # NEW: Core domain logic
│   ├── Models/                    # ActivityPub models
│   │   ├── activities/            # Activity types (Create, Follow, etc.)
│   │   ├── actors/                # Actor types
│   │   ├── collections/           # Collection types
│   │   └── webfinger/             # WebFinger response models
│   ├── Interfaces/                # Interfaces (reorganized)
│   ├── Events/                    # Domain events
│   └── Options/                   # Configuration options
├── Infrastructure/                # NEW: Infrastructure concerns
│   ├── Data/                      # Database, EF Core
│   │   ├── Context/
│   │   ├── Entities/
│   │   ├── Repositories/
│   │   └── Migrations/
│   ├── Caching/                   # Cache implementations
│   ├── Metrics/                   # Metrics collection
│   ├── Telemetry/                 # OpenTelemetry setup
│   ├── Logging/                   # Logging configuration
│   └── Monitoring/                # Health checks
├── Services/                      # Business logic services
│   ├── ActivityProcessing/        # Activity processing
│   ├── Federation/                # Federation logic
│   ├── WebFinger/                 # WebFinger resolution
│   ├── Security/                  # Signature verification
│   └── Background/                # Background services
├── API/                           # NEW: API layer
│   ├── Controllers/
│   │   ├── v1/                    # API versioning
│   │   ├── v2/                    # Future versions
│   │   └── Dashboard/             # Admin endpoints
│   ├── Middleware/                # HTTP middleware
│   └── Filters/                   # Action filters
├── Plugins/                       # Plugin system
├── Implementations/               # Interface implementations
└── Program.cs
```

#### ActivityPub.Cli (NEW)
```
src/ActivityPub.Cli/
├── Commands/                      # CLI commands
│   ├── actor/                     # Actor management
│   ├── activity/                  # Activity operations
│   ├── federation/                # Federation management
│   └── status/                    # Health checks
├── Services/                      # CLI services
├── Models/                        # CLI models
└── Program.cs
```

#### ActivityPub.Admin (NEW)
```
src/ActivityPub.Admin/
├── Pages/                         # Razor Pages
│   ├── Index.cshtml
│   ├── Actors/
│   ├── Activities/
│   ├── Federation/
│   └── Metrics/
├── wwwroot/                       # Static files
│   ├── css/
│   ├── js/
│   └── images/
├── Services/                      # Admin services
└── Program.cs
```

### 2. Tests Structure (`tests/`)

```
tests/ActivityPub.Tests/
├── UnitTests/                     # Unit tests (organized by layer)
│   ├── Core/
│   │   ├── Models/
│   │   ├── Services/
│   │   └── Repositories/
│   ├── Services/                  # Service unit tests
│   ├── Infrastructure/            # Infrastructure mocks
│   └── Integration/               # Integration mocks
├── IntegrationTests/              # Integration tests (organized by feature)
│   ├── ActivityExchange/
│   ├── Federation/
│   ├── WebFinger/
│   ├── HttpSignature/
│   ├── InboxProcessing/
│   └── MultiInstance/
├── ScaleTests/                    # Performance tests
│   ├── Database/
│   ├── Cache/
│   ├── ActivityProcessing/
│   └── Federation/
├── fixtures/                      # Test fixtures and data
├── helpers/                       # Test helper utilities
└── AssemblyInfo.cs
```

### 3. Sample Projects (`samples/`)

```
samples/
├── quickstart/                    # NEW: Minimal example
│   ├── Program.cs
│   ├── appsettings.json
│   └── README.md
├── advanced/                      # NEW: Complex examples
│   ├── with-database/
│   ├── with-federation/
│   └── with-plugins/
└── existing/                      # Migrated from SampleProjects/
    ├── FederationApp/
    ├── BotApp/
    ├── DemoApp/
    └── ConsoleClient/
```

---

## Migration Steps

### Phase 1: Foundation (Week 1)
1. Create new directory structure
2. Move and reorganize core files
3. Update all project references
4. Run `dotnet build` - fix errors
5. Run `dotnet test` - verify tests

### Phase 2: Documentation (Week 2)
1. Create new README.md
2. Move and rename existing docs
3. Create API reference
4. Write contribution guide
5. Add issue templates

### Phase 3: Tooling (Week 3)
1. Create build scripts
2. Set up CI/CD pipelines
3. Configure code quality tools
4. Update documentation

### Phase 4: Cleanup (Week 4)
1. Remove old directories
2. Delete redundant files
3. Consolidate solution files
4. Final testing

---

## File Cleanup Actions

### Remove/Deprecate
- ❌ `ActivityPub.Core/Class1.cs` - Empty placeholder
- ❌ `ActivityPub.Core/RejectActivityHandler.cs` - Empty file
- ❌ `ActivityPub.slnx` - Solution filter (not needed)
- ❌ Duplicate README.md files in SampleProjects

### Rename
- `ARCHITECTURE_GUIDE.md` → `docs/architecture.md`
- `TESTING_GUIDE.md` → `docs/testing.md`
- `ACTIVITYPUB_STATE_REPORT.md` → `docs/state-report.md`
- `IntegrationTests/Scale/` → `ScaleTests/`

### Consolidate
- Merge `SampleProjects/` → `samples/`
- Consolidate all `.sln` files into single solution
- Unify `UnitTests` and `IntegrationTests` folder structure

---

## Benefits

1. **Clear Separation of Concerns** - Each layer has dedicated folder
2. **Scalable Structure** - Easy to add new features
3. **Better Testing** - Organized by test type and feature
4. **Improved Documentation** - Centralized docs folder
5. **Maintainability** - Easier to find and modify code
6. **Onboarding** - Clear structure for new contributors

---

## Next Steps

1. Review and approve this plan
2. Create backup of current branch
3. Execute migration in phases
4. Verify build and tests after each phase
5. Update documentation
6. Announce changes to team
