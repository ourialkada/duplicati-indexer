#!/bin/bash

# Create a backup source using the API endpoint
# This replaces the Python script approach with a direct HTTP API call

API_URL="${API_URL:-http://localhost:8080}"

curl -X POST "${API_URL}/api/backup-sources" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "TestIndexedEmails",
    "duplicatiBackupId": "id1",
    "encryptionPassword": "easy1234",
    "targetUrl": "file:///backupdest"
  }'
