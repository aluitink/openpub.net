# FederationApp

A sample ActivityPub application demonstrating federation between multiple instances.

## Overview

The FederationApp demonstrates:
- Managing multiple ActivityPub instances
- Sending activities between instances
- UI dashboard for instance management
- Background service for automated delivery

## Features

- **Instance Management**: Add, remove, and track connected instances
- **Activity Sending**: Send ActivityPub activities to remote inboxes
- **Delivery Tracking**: Monitor delivery status across instances
- **Background Service**: Automated delivery of activities

## Project Structure

```
FederationApp/
├── Federation/
│   ├── InstanceManager.cs       # Manages connected instances
│   ├── FederationService.cs     # Handles activity federation
│   ├── FederationController.cs  # API endpoints
│   ├── ActivityDeliveryService.cs # Background delivery service
│   └── FederationAppExtensions.cs # DI setup
├── Pages/
│   ├── Index.cshtml             # UI dashboard
│   └── Index.cshtml.cs          # UI code-behind
├── Program.cs                   # Application entry point
└── appsettings.json             # Configuration
```

## API Endpoints

- `GET /api/federation/instances` - List all connected instances
- `POST /api/federation/instances` - Add a new instance
- `DELETE /api/federation/instances/{domain}` - Remove an instance
- `POST /api/federation/send` - Send an activity to an instance
- `GET /api/federation/delivery/status` - Get delivery status

## Running

```bash
cd SampleProjects/FederationApp
dotnet run
```

Then open `http://localhost:5000` in your browser.

## Configuration

Edit `appsettings.json` to configure:

```json
{
  "ActivityPub": {
    "Domain": "localhost",
    "UserPath": "/users",
    "EnableFederation": true
  }
}
```

## Federation Protocol

The app implements:
- ActivityPub activity sending
- HTTP POST to remote inboxes
- JSON-LD serialization
- Background delivery queue

## Dependencies

- ActivityPub.Core
- Entity Framework Core (InMemory)
- HttpClient
- Background Services
