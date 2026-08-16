# ActivityPub.NET

A modern, high-performance implementation of the ActivityPub protocol in .NET for building decentralized social networks and federated applications.

[![Build Status](https://img.shields.io/azure-devops/build/)](https://dev.azure.com/)
[![Tests](https://img.shields.io/badge/tests-625-green)](https://github.com/yourorg/activitypub-dotnet)
[![License](https://img.shields.io/github/license/yourorg/activitypub-dotnet)](LICENSE)
[![Discord](https://img.shields.io/discord/123456789)](https://discord.gg/xyz)

## Overview

ActivityPub.NET is a comprehensive library for implementing the [ActivityPub](https://www.w3.org/TR/activitypub/) protocol in .NET applications. It provides everything you need to build federated social media platforms, microblogging services, and decentralized communication networks.

### Key Features

- ✅ **Full ActivityPub Compliance** - Implements W3C ActivityPub specification
- ✅ **Activity Streams 2.0** - Complete JSON-LD support for activity streams
- ✅ **HTTP Signatures** - Secure request authentication
- ✅ **WebFinger** - User discovery and identity resolution
- ✅ **Federation** - Cross-platform federation with Mastodon, Pleroma, Friendica
- ✅ **Background Processing** - Asynchronous activity handling
- ✅ **Caching** - High-performance caching layer
- ✅ **Telemetry** - Built-in metrics and monitoring
- ✅ **Tested** - 625+ unit and integration tests

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- SQLite database (or your preferred database provider)
- Git

### Installation

```bash
git clone https://github.com/yourorg/activitypub-dotnet.git
cd activitypub-dotnet
```

### Quick Start

```csharp
builder.Services.AddActivityPub()
    .AddInboxProcessor()
    .AddOutboxProcessor()
    .AddWebFinger()
    .AddFederation();

var app = builder.Build();
app.UseActivityPub();
app.Run();
```

## Documentation

- [Overview](docs/overview.md) - Project architecture and design
- [Architecture](docs/architecture.md) - System architecture details
- [Testing](docs/testing.md) - Testing strategy and guidelines
- [Contributing](docs/contributing.md) - How to contribute
- [API Reference](docs/api-reference/) - Full API documentation
- [Directory Structure](docs/directory-structure.md) - Project folder structure
- [Migration Guide](docs/migration-guide.md) - Migrating from old structure
- [Deployment](docs/deployment.md) - Deploying Fediblog with Docker

## Directory Structure

```
/workspace/
├── src/                           # Source code
│   ├── ActivityPub.Core/          # Core library
│   │   ├── Core/                  # Domain models and interfaces
│   │   ├── Services/              # Business logic
│   │   ├── API/                   # API layer (controllers, middleware)
│   │   ├── Infrastructure/        # Data, caching, logging, metrics
│   │   └── Plugins/               # Plugin system
│   ├── ActivityPub.Cli/           # Command-line tool
│   └── ActivityPub.Admin/         # Admin dashboard
├── ActivityPub.Tests/             # Test suite
│   ├── UnitTests/                 # Unit tests
│   ├── IntegrationTests/          # Integration tests
│   └── ScaleTests/                # Performance tests
├── samples/                       # Sample applications
├── docs/                          # Documentation
│   ├── overview.md
│   ├── architecture.md
│   ├── testing.md
│   ├── migration-guide.md
│   ├── directory-structure.md
│   ├── api-reference/
│   └── contributing.md
└── scripts/                       # Build and deployment scripts
    ├── build.sh
    ├── test.sh
    └── publish.sh
```

## Development

### Building

```bash
./scripts/build.sh
```

### Running Tests

```bash
./scripts/test.sh
```

### Running the Sample App

```bash
cd samples/quickstart
dotnet run
```

## Fediblog - Social Platform Demo

[Fediblog](src/ActivityPub.WebUI/) is a Mastodon-like microblogging application built on ActivityPub.NET. It demonstrates a complete ActivityPub-powered social platform:

### Features

- **User Authentication** - Registration, login, cookie-based sessions
- **Compose & Timeline** - Create notes, view home timeline with chronological feed
- **Follows** - Follow other users locally and federated accounts
- **Interactions** - Like, reply to, and boost (repost) notes
- **Profiles** - Customizable profiles with avatar, banner, bio, and stats
- **Notifications** - See follows, likes, boosts, and replies
- **Search** - Find users and notes by keyword
- **Hashtags** - Discover content by hashtag
- **Rate Limiting** - Protected compose and follow endpoints
- **Responsive Design** - Mobile-friendly CSS

### Quick Start

```bash
cd src/ActivityPub.WebUI
docker compose up -d
# Visit http://localhost:8080
```

### Stack

- ASP.NET Core MVC with Razor views
- SQLite for both Identity and ActivityPub databases
- ActivityPub federation with HTTP signature verification
- Docker deployment with docker-compose

## Contributing

We welcome contributions! Please read our [Contributing Guide](docs/contributing.md) for details on:

- Setting up your development environment
- Running tests
- Code style guidelines
- Pull request process

## Community

- Join our [Discord](https://discord.gg/xyz) community
- Report issues on [GitHub Issues](https://github.com/yourorg/activitypub-dotnet/issues)
- Follow us on Twitter [@activitypubnet](https://twitter.com/activitypubnet)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Thanks to the ActivityPub and ActivityStreams communities for their excellent specifications
- Built with [.NET](https://dotnet.microsoft.com/) and [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
