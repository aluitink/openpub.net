# ActivityPub.Core Docker Image

## Build
```bash
docker build -t activitypub:latest .
```

## Run
```bash
docker run -p 8080:80 activitypub:latest
```

## Environment Variables
- `ACTIVITYPUB_DOMAIN`: Domain for ActivityPub (default: localhost)
- `ACTIVITYPUB_USERPATH`: User path prefix (default: /users)
- `ACTIVITYPUB_PORT`: Port to listen on (default: 80)
- `DATABASE_CONNECTION`: Database connection string (default: in-memory)
- `REDIS_CONNECTION`: Redis connection string for distributed caching (optional)

## Volume Mounts
- `/app/data`: Data directory for database files

## Health Check
```bash
curl http://localhost:8080/.well-known/host-meta
```
