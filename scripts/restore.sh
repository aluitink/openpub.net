#!/bin/bash
set -e

echo "Database Restore Script"
echo "======================="

if [ $# -lt 1 ]; then
    echo "Usage: $0 <backup-file>"
    exit 1
fi

BACKUP_FILE=$1
DATABASE_FILE="./data/activitypub.db"

if [ ! -f "${BACKUP_FILE}" ]; then
    echo "Backup file not found: ${BACKUP_FILE}"
    exit 1
fi

# Stop the application
echo "Stopping application..."
docker-compose stop activitypub

# Restore the database
echo "Restoring database from: ${BACKUP_FILE}"
mkdir -p ./data
cp ${BACKUP_FILE} ${DATABASE_FILE}

# Start the application
echo "Starting application..."
docker-compose start activitypub

echo "Restore completed successfully!"
