#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

API_PORT="${API_PORT:-5080}"

echo "=== ELIFOOT CLASSIC MULTIPLAYER - DEV LAUNCHER (LINUX/MACOS) ==="

if [ ! -f "tools/seed-generator/seed-128-clubs.json" ]; then
  echo "Gerando pacote inicial de 128 clubes..."
  dotnet run --project tools/seed-generator/Elifoot.SeedGenerator.csproj -c Release -- --clubs 128 --seed 199801 --output tools/seed-generator/seed-128-clubs.json
fi

export ASPNETCORE_ENVIRONMENT="LocalDev"
export ASPNETCORE_URLS="http://localhost:${API_PORT}"

echo "Iniciando API autoritativa (.NET 10 + SignalR + PostgreSQL) em http://localhost:${API_PORT} ..."
dotnet run --project src/Elifoot.Api/Elifoot.Api.csproj -c Release --urls "http://localhost:${API_PORT}"
