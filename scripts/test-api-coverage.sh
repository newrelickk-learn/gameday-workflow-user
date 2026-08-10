#!/bin/bash
set -e

# APIテストをcurlで実行し、カバレッジを取得するスクリプト
# 注意: curlベースのテストではコードカバレッジは取得できません
# このスクリプトは統合テストとして実行されます

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

echo "Running API integration tests with curl..."
echo "Note: Code coverage is not available for curl-based tests."
echo ""

# 通常のAPIテストを実行
./scripts/test-api.sh
