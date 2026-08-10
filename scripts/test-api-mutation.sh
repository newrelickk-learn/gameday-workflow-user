#!/bin/bash
set -e

# Mutation Testingを実行するスクリプト
# docker-compose経由でMutation Testingを実行します

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

echo "Running mutation testing..."
docker-compose run --rm test-mutation

echo ""
echo "Mutation test report generated in src/UserService.Api/StrykerOutput/"
