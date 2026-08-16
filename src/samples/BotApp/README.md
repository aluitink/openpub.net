# ActivityPub BotApp Sample

A sample ActivityPub bot application demonstrating auto-respond and relay functionality.

## Features

- **Auto-respond**: Automatically responds to mentions and follows
- **Relay**: Forwards activities to followers
- **Configuration**: Customizable via appsettings.json
- **Background services**: Runs as a background service

## Project Structure

```
BotApp/
├── BotApp.csproj          # Project file
├── Program.cs             # Main entry point
├── Bot/
│   ├── AutoResponder.cs   # Auto-respond logic
│   └── RelayService.cs    # Relay functionality
├── appsettings.json       # Configuration
└── README.md              # This file
```

## Configuration

Edit `appsettings.json` to configure the bot:

```json
{
  "ActivityPub": {
    "Domain": "localhost",
    "UserPath": "/users",
    "Port": 5000,
    "EnableFederation": false
  },
  "Bot": {
    "Username": "bot",
    "AutoRespondEnabled": true,
    "RelayEnabled": true,
    "RelayIntervalSeconds": 30
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## Building and Running

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run
```

### Run with custom configuration

```bash
dotnet run -- --configuration Production
```

## How It Works

### Auto-Respond

The `AutoResponder` class handles incoming activities:

- **Follow**: Auto-accepts follow requests
- **Create**: Replies to mentions containing `@bot`
- **Like/Announce**: Logs the activity

### Relay Service

The `RelayService` class:

- Monitors the bot's outbox
- Forwards new activities to followers
- Runs as a background service with configurable intervals

## Testing

Run tests with:

```bash
dotnet test
```

## Requirements

- .NET 10.0 SDK
- ActivityPub.Core library

## License

MIT
