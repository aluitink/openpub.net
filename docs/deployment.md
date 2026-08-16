# ActivityPub.NET - Deployment Guide

## Fediblog Deployment

Fediblog is a Mastodon-like microblogging application built on ActivityPub.NET, located at `src/ActivityPub.WebUI/`.

## Prerequisites

- Docker 24+ and Docker Compose v2 (for container deployment)
- OR .NET 10 SDK (for standalone deployment)

## Docker Deployment

### Quick Start

```bash
cd src/ActivityPub.WebUI
docker compose up -d
```

This starts Fediblog on `http://localhost:8080`.

### Configuration

Create a `.env` file in `src/ActivityPub.WebUI/`:

```env
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:80
ConnectionStrings__DefaultConnection=Data Source=/data/app.db
ConnectionStrings__ActivityPubConnection=Data Source=/data/ap.db
```

### Volumes

Persistent data is stored in the `fediblog-data` Docker volume at `/data` inside the container.

### Production HTTPS

For production, add a reverse proxy (e.g., Nginx, Traefik) in front of the container:

```yaml
services:
  reverse-proxy:
    image: nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
      - ./certs:/etc/nginx/certs
    depends_on:
      - fediblog

  fediblog:
    build:
      context: ..
      dockerfile: ActivityPub.WebUI/Dockerfile
    environment:
      - ASPNETCORE_URLS=http://+:80
```

## Standalone Deployment

```bash
dotnet publish src/ActivityPub.WebUI -c Release -o /app
cd /app
dotnet ActivityPub.WebUI.dll
```

### Configuration via appsettings.Production.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/data/fediblog.db",
    "ActivityPubConnection": "Data Source=/data/fediblog_ap.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | App environment |
| `ASPNETCORE_URLS` | `http://+:80` | Listening URLs |
| `ConnectionStrings__DefaultConnection` | `fediblog.db` | Identity database path |
| `ConnectionStrings__ActivityPubConnection` | `fediblog_ap.db` | ActivityPub database path |

## Rate Limiting

Rate limiting is enabled by default on compose and follow endpoints:
- **Window:** 1 minute
- **Max requests:** 20 per window
- **Paths:** `/compose/post`, `/follow/follow`

## Database Migration

SQLite databases are auto-created on first run. For migrations:

```bash
dotnet ef database update --project src/ActivityPub.WebUI
```

## Backup

```bash
docker exec fediblog-fediblog-1 cp /data/app.db /data/app.db.bak
docker exec fediblog-fediblog-1 cp /data/ap.db /data/ap.db.bak
```
