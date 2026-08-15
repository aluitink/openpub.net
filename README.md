# ActivityPub.NET

A modern, high-performance implementation of the ActivityPub protocol in .NET for building decentralized social networks and federated applications.

[![Build Status](https://img.shields.io/azure-devops/build/)](https://dev.azure.com/)
[![Tests](https://img.shields.io/badge/tests-386-green)](https://github.com/yourorg/activitypub-dotnet)
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
- ✅ **Tested** - 386+ unit and integration tests

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

## Directory Structure

```
/workspace/
├── src/              # Source code
│   ├── ActivityPub.Core/   # Core library
│   ├── ActivityPub.Cli/    # Command-line tool
│   └── ActivityPub.Admin/  # Admin dashboard
├── tests/            # Test suite
├── samples/          # Sample applications
├── docs/             # Documentation
└── scripts/          # Build and deployment scripts
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
