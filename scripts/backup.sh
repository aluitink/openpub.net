#!/bin/bash
set -e

echo "Database Backup Script"
echo "======================"

BACKUP_DIR=${BACKUP_DIR:-./backups}
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/activitypub_${TIMESTAMP}.db"

# Create backup directory if it doesn't exist
mkdir -p ${BACKUP_DIR}

# Backup the database
echo "Creating backup: ${BACKUP_FILE}"

# For SQLite
if [ -f "./data/activitypub.db" ]; then
    cp ./data/activitypub.db ${BACKUP_FILE}
    echo "Backup completed: ${BACKUP_FILE}"
else
    echo "No database file found at ./data/activitypub.db"
    exit 1
fi

# Cleanup old backups (keep last 7 days)
find ${BACKUP_DIR} -name "activitypub_*.db" -mtime +7 -delete

echo "Backup completed successfully!"
