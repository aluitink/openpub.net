# ActivityPub Demo App

A .NET 10.0 sample project demonstrating the ActivityPub.Core library functionality.

## Features

- **Key Generation**: Generate RSA key pairs for HTTP signatures
- **Actor Management**: Create and list ActivityPub actors
- **Activity Submission**: Submit activities to the in-memory database
- **Status Check**: View service health and version info
- **Minimal UI**: Single-page application for easy interaction

## Requirements

- .NET 10.0 SDK
- Web browser (for UI)

## Quick Start

```bash
cd SampleProjects/DemoApp
dotnet run
```

The application will start on `http://localhost:8080`.

## API Endpoints

### `/demo/keys`
GET - Generate a new RSA key pair

Response:
```json
{
  "privateKey": "-----BEGIN RSA PRIVATE KEY-----...",
  "publicKey": "-----BEGIN PUBLIC KEY-----..."
}
```

### `/demo/actors`
GET - List all actors

POST - Create a new actor
```json
"username"
```

### `/demo/activities`
POST - Submit a new activity
```json
{
  "activityId": "unique-id",
  "jsonData": "{\"type\":\"Create\",\"content\":\"Hello\"}"
}
```

### `/demo/status`
GET - Service status

Response:
```json
{
  "service": "ActivityPub Demo",
  "version": "1.0.0",
  "status": "Running"
}
```

## Project Structure

```
DemoApp/
├── Program.cs              # Application entry point
├── appsettings.json        # Configuration
├── appsettings.Development.json
├── DemoApp.csproj          # Project file
└── wwwroot/
    ├── index.html          # UI
    ├── styles.css          # Styling
    └── script.js           # Client-side logic
```

## Configuration

Edit `appsettings.json` to customize:
- `ActivityPub.Domain` - Domain name (default: localhost)
- `ActivityPub.UserPath` - User path (default: /users)
- `Logging.LogLevel` - Log verbosity

## Database

The app uses Entity Framework Core in-memory database by default. Data is lost on restart.

For persistent storage, modify the connection string in `appsettings.json` to use SQLite.

## Troubleshooting

**Port already in use**: The app tries to use port 8080. Change the port in your project configuration or kill the existing process.

**CORS errors**: The app doesn't enforce CORS for development. For production, configure CORS policies.

## Next Steps

- Add SQLite persistence
- Implement HTTP signature signing
- Add federation testing tools
- Enhance the UI with real-time updates
