# ActivityPub.NET - Project Overview

## What is ActivityPub.NET?

ActivityPub.NET is a modern, production-ready implementation of the ActivityPub protocol built on the .NET platform. It enables developers to create federated social networking applications that can communicate with other ActivityPub-compatible platforms like Mastodon, Pleroma, and Friendica.

## Architecture

ActivityPub.NET follows a clean architecture approach with clear separation of concerns:

### Core Components

1. **Activity Processing** - Handles incoming and outgoing activities
2. **Federation** - Manages server-to-server communication
3. **WebFinger** - Provides user identity and discovery
4. **Security** - Implements HTTP signatures for authentication
5. **Storage** - EF Core-based data persistence
6. **Caching** - High-performance caching layer
7. **Background Services** - Asynchronous activity queue processing

### Layered Architecture

```
┌─────────────────────────────────────────┐
│         API Layer (Controllers)         │
├─────────────────────────────────────────┤
│        Service Layer (Business)         │
├─────────────────────────────────────────┤
│      Repository Layer (Data Access)     │
├─────────────────────────────────────────┤
│       Infrastructure (External)         │
└─────────────────────────────────────────┘
```

## Key Features

- **ActivityPub Protocol** - Full W3C specification compliance
- **ActivityStreams 2.0** - Complete JSON-LD support
- **Federation** - Cross-platform compatibility
- **Security** - HTTP Signature verification
- **Scalability** - Background processing and caching
- **Observability** - Metrics, logging, and telemetry

## Technology Stack

- **Language**: C# 13 / .NET 10
- **Database**: Entity Framework Core (SQLite)
- **Serialization**: System.Text.Json
- **Testing**: xUnit
- **Build**: MSBuild

## Use Cases

- Federated microblogging platforms
- Decentralized social networks
- Community forums and discussion boards
- Real-time activity aggregation
- Cross-platform communication bridges

## Getting Started

See the [Contributing Guide](contributing.md) for development setup instructions.

## Next Steps

- Read the [Architecture Guide](architecture.md) for detailed system design
- Check [Testing](testing.md) for testing strategies
- Explore sample applications in `samples/`
