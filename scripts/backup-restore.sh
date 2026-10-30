#!/usr/bin/env bash
set -euo pipefail

ACTION="${1:-backup}"
API_URL="${API_URL:-http://localhost:5080}"
WORLD_ID="${WORLD_ID:-}"
FILE_PATH="${FILE_PATH:-backups/world-backup.json}"

mkdir -p "$(dirname "$FILE_PATH")"

if [ "$ACTION" = "backup" ]; then
  if [ -z "$WORLD_ID" ]; then
    echo "Informe WORLD_ID=<guid> para exportar."
    exit 1
  fi
  curl -sS "${API_URL}/api/worlds/${WORLD_ID}/editor/export-json" -o "$FILE_PATH"
  echo "Backup salvo em: $FILE_PATH"
elif [ "$ACTION" = "restore" ]; then
  curl -sS -X POST "${API_URL}/api/worlds/editor/import-package?newWorldName=Mundo_Restaurado" \
    -H "Content-Type: application/json" \
    --data-binary "@${FILE_PATH}"
  echo ""
  echo "Mundo restaurado com sucesso a partir de: $FILE_PATH"
fi
