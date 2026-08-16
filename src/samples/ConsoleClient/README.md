# ActivityPub Console Client

A minimal CLI client for ActivityPub operations.

## Features

- Send activities (Follow, Like, Announce, etc.)
- Generate RSA key pairs
- Discover actor information
- Query WebFinger directory
- Verify activity signatures

## Building

```bash
dotnet build
```

## Usage

```bash
# Send an activity
dotnet run -- send --type Follow --actor https://localhost/users/me --object https://remote.actor/users/other --url https://remote.actor/inbox

# Generate keys
dotnet run -- generate-keys

# Discover an actor
dotnet run -- discover --resource user@example.com

# Query WebFinger
dotnet run -- webfinger --resource user@example.com

# Verify activity
dotnet run -- verify --activity activity.json --signature "keyId=...,signature=..." --public-key "-----BEGIN PUBLIC KEY-----..."
```
