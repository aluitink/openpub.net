# Environment Variables Reference

## Core Configuration
| Variable | Description | Default |
|----------|-------------|---------|
| `ACTIVITYPUB_DOMAIN` | Domain for ActivityPub endpoints | `localhost` |
| `ACTIVITYPUB_USERPATH` | User path prefix | `/users` |
| `ACTIVITYPUB_PORT` | Port to listen on | `80` |

## Database
| Variable | Description | Default |
|----------|-------------|---------|
| `DATABASE_CONNECTION` | Database connection string | In-memory |

## Caching
| Variable | Description | Default |
|----------|-------------|---------|
| `REDIS_CONNECTION` | Redis connection string for distributed caching | `null` (in-memory cache) |

## Rate Limiting
| Variable | Description | Default |
|----------|-------------|---------|
| `RATELIMIT_WINDOW` | Time window in minutes | `1` |
| `RATELIMIT_MAXREQUESTS` | Maximum requests per window | `100` |

## Security
| Variable | Description | Default |
|----------|-------------|---------|
| `SECURITY_CSP_ENABLED` | Enable Content Security Policy | `true` |
| `SECURITY_HSTS_ENABLED` | Enable Strict Transport Security | `true` |

## Logging
| Variable | Description | Default |
|----------|-------------|---------|
| `LOG_LEVEL` | Logging level (Trace, Debug, Info, Warning, Error) | `Info` |
| `LOG_FILE` | Log file path | `null` (console only) |
