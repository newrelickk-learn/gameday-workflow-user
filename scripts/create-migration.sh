#!/bin/bash
set -e

# マイグレーションを作成するスクリプト

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

MIGRATION_NAME=${1:-"InitialCreate"}

echo "Creating migration: $MIGRATION_NAME"
docker-compose run --rm migrate dotnet ef migrations add "$MIGRATION_NAME" --project src/UserService.Api

echo ""
echo "Migration created successfully!"
echo "To apply the migration, run: make migrate"

