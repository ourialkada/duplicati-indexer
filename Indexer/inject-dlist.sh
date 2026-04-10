#!/bin/bash

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <dlist-filename>"
    exit 1
fi

curl -X POST http://localhost:8080/api/messages/backup-version-created \
  -H "Content-Type: application/json" \
  -d '{"BackupId": "id1", "DlistFilename": "'$1'"}'