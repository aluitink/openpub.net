# Directory Structure

## Overview

This document describes the directory structure of the ActivityPub.Core project.

## Root Directory

```
/workspace/
├── src/                           # Source code (all code projects)
│   ├── ActivityPub.Core/          # Core library
│   ├── ActivityPub.Admin/         # Admin dashboard
│   ├── ActivityPub.Cli/           # Command-line tool
│   ├── ActivityPub.WebUI/         # Fediblog web UI
│   ├── ActivityPub.Tests/         # Test projects
│   ├── ActivityPub.Benchmarks/    # BenchmarkDotNet benchmarks
│   └── samples/                   # Sample applications
├── docs/                          # Documentation
├── scripts/                       # Build and deployment scripts
├── .github/                       # GitHub workflows
├── ActivityPub.sln                # Main solution file
├── README.md                      # Project overview
└── PLAN.md                        # Project plan
```

## Source Structure (`src/`)

### ActivityPub.Core (`src/ActivityPub.Core/`)

```
src/ActivityPub.Core/
├── Core/                          # NEW: Core domain logic
│   ├── Models/                    # ActivityPub models
│   │   ├── activities/            # Activity types (Create, Follow, etc.)
│   │   ├── actors/                # Actor types (Person, Application, etc.)
│   │   ├── collections/           # Collection types (OrderedCollection, etc.)
│   │   └── webfinger/             # WebFinger response models
│   ├── Interfaces/                # Interfaces (reorganized)
│   ├── Events/                    # Domain events
│   └── Options/                   # Configuration options
├── Infrastructure/                # Infrastructure concerns
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

### ActivityPub.Cli (`src/ActivityPub.Cli/`) - NEW

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

### ActivityPub.Admin (`src/ActivityPub.Admin/`) - NEW

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

## Tests Structure

```
src/ActivityPub.Tests/
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

## Samples Structure

```
src/samples/
├── quickstart/                    # Minimal example
│   ├── Program.cs
│   ├── appsettings.json
│   └── README.md
├── advanced/                      # Complex examples
│   ├── with-database/
│   ├── with-federation/
│   └── with-plugins/
└── existing/                      # Migrated from SampleProjects/
    ├── FederationApp/
    ├── BotApp/
    ├── DemoApp/
    └── ConsoleClient/
```

## Documentation Structure

```
docs/
├── overview.md                    # Project overview
├── architecture.md                # Architecture guide
├── testing.md                     # Testing guide
├── state-report.md                # Current state report
├── migration-guide.md             # Migration guide
├── contributing.md                # Contribution guide
├── api-reference/                 # API documentation
│   └── index.md                  # API index
└── faq.md                         # Frequently asked questions
```

## Scripts Structure

```
scripts/
├── build.sh                       # Cross-platform build script
├── test.sh                        # Test runner script
├── publish.sh                     # Publish script
└── ci/                            # CI/CD configurations
    └── .github/workflows/ci.yml  # GitHub Actions workflow
```

## File Organization Rules

1. **Core Models** → `src/ActivityPub.Core/Core/Models/`
2. **Business Logic** → `src/ActivityPub.Core/Services/`
3. **API Layer** → `src/ActivityPub.Core/API/`
4. **Infrastructure** → `src/ActivityPub.Core/Infrastructure/`
5. **Tests** → `src/ActivityPub.Tests/[Unit|Integration|Scale]Tests/`
