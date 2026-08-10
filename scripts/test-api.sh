#!/bin/bash
set -e

# APIテストをcurlで実行するスクリプト
# docker-composeでAPIを起動し、curlでエンドポイントをテストします

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$PROJECT_ROOT"

API_URL="http://localhost:8001"
TEST_RESULT=0

# 色の定義
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# テスト結果を表示する関数
print_test_result() {
    local test_name=$1
    local status_code=$2
    local expected_code=$3
    
    if [ "$status_code" -eq "$expected_code" ]; then
        echo -e "${GREEN}✓${NC} $test_name (Status: $status_code)"
    else
        echo -e "${RED}✗${NC} $test_name (Expected: $expected_code, Got: $status_code)"
        TEST_RESULT=1
    fi
}

# APIが起動するまで待機
wait_for_api() {
    echo "Waiting for API to be ready..."
    local max_attempts=30
    local attempt=0
    
    while [ $attempt -lt $max_attempts ]; do
        if curl -s -f "$API_URL/health" > /dev/null 2>&1; then
            echo "API is ready!"
            return 0
        fi
        attempt=$((attempt + 1))
        sleep 1
    done
    
    echo -e "${RED}Error: API did not become ready within $max_attempts seconds${NC}"
    exit 1
}

# docker-composeでAPIを起動
echo "Starting services with docker-compose..."
# docker-compose up -d

# APIが起動するまで待機
# wait_for_api

echo ""
echo "=========================================="
echo "Running API Tests with curl"
echo "=========================================="
echo ""

# テスト1: ログイン成功（engineer）
echo "Test 1: Login with valid credentials (engineer)"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_URL/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"email":"engineer@example.com","password":"password"}')
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')

if [ "$HTTP_CODE" -eq 200 ]; then
    TOKEN=$(echo "$BODY" | grep -o '"token":"[^"]*' | cut -d'"' -f4)
    echo -e "${GREEN}✓${NC} Login successful (Status: $HTTP_CODE)"
    echo "  Token: ${TOKEN:0:50}..."
else
    echo -e "${RED}✗${NC} Login failed (Expected: 200, Got: $HTTP_CODE)"
    echo "  Response: $BODY"
    TEST_RESULT=1
fi

# テスト2: ログイン失敗（無効なパスワード）
echo ""
echo "Test 2: Login with invalid password"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_URL/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"email":"engineer@example.com","password":"wrongpassword"}')
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
print_test_result "Login with invalid password" "$HTTP_CODE" 401

# テスト3: ログイン失敗（存在しないメールアドレス）
echo ""
echo "Test 3: Login with non-existent email"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_URL/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"email":"nonexistent@example.com","password":"password"}')
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
print_test_result "Login with non-existent email" "$HTTP_CODE" 401

# テスト4: ユーザー情報取得（有効なトークン）
if [ -n "$TOKEN" ]; then
    echo ""
    echo "Test 4: Get user by ID with valid token"
    RESPONSE=$(curl -s -w "\n%{http_code}" -X GET "$API_URL/users/28151" \
        -H "Authorization: Bearer $TOKEN")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    
    if [ "$HTTP_CODE" -eq 200 ]; then
        USER_ID=$(echo "$BODY" | grep -o '"id":"[^"]*' | cut -d'"' -f4)
        USER_EMAIL=$(echo "$BODY" | grep -o '"email":"[^"]*' | cut -d'"' -f4)
        echo -e "${GREEN}✓${NC} Get user successful (Status: $HTTP_CODE)"
        echo "  User ID: $USER_ID"
        echo "  User Email: $USER_EMAIL"
    else
        echo -e "${RED}✗${NC} Get user failed (Expected: 200, Got: $HTTP_CODE)"
        echo "  Response: $BODY"
        TEST_RESULT=1
    fi
else
    echo ""
    echo -e "${YELLOW}⚠${NC} Skipping Test 4: No token available"
    TEST_RESULT=1
fi

# テスト5: ユーザー情報取得（無効なトークン）
echo ""
echo "Test 5: Get user by ID with invalid token"
RESPONSE=$(curl -s -w "\n%{http_code}" -X GET "$API_URL/users/28151" \
    -H "Authorization: Bearer invalid_token")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
print_test_result "Get user with invalid token" "$HTTP_CODE" 401

# テスト6: ユーザー情報取得（トークンなし）
echo ""
echo "Test 6: Get user by ID without token"
RESPONSE=$(curl -s -w "\n%{http_code}" -X GET "$API_URL/users/28151")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
print_test_result "Get user without token" "$HTTP_CODE" 401

# テスト7: ユーザー情報取得（存在しないユーザーID）
if [ -n "$TOKEN" ]; then
    echo ""
    echo "Test 7: Get user with non-existent ID"
    RESPONSE=$(curl -s -w "\n%{http_code}" -X GET "$API_URL/users/99999" \
        -H "Authorization: Bearer $TOKEN")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    print_test_result "Get user with non-existent ID" "$HTTP_CODE" 404
fi

# テスト8: 異なるロールでログイン
echo ""
echo "Test 8: Login with different roles"
for role in "director" "accounting" "manager" "engineer"; do
    EMAIL="${role}@example.com"
    RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_URL/auth/login" \
        -H "Content-Type: application/json" \
        -d "{\"email\":\"$EMAIL\",\"password\":\"password\"}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    
    if [ "$HTTP_CODE" -eq 200 ]; then
        ROLE=$(echo "$RESPONSE" | sed '$d' | grep -o '"role":"[^"]*' | cut -d'"' -f4)
        echo -e "${GREEN}✓${NC} Login as $role (Status: $HTTP_CODE, Role: $ROLE)"
    else
        echo -e "${RED}✗${NC} Login as $role failed (Status: $HTTP_CODE)"
        TEST_RESULT=1
    fi
done

# テスト9: 内部サービス用API Key認証（有効なAPI Key）
echo ""
echo "Test 9: Get user with valid API Key (Internal Service)"
API_KEY="InternalServiceApiKeyForGameDayWorkflow2024!"
RESPONSE=$(curl -s -w "\n%{http_code}" -X GET "$API_URL/users/28151" \
    -H "X-API-Key: $API_KEY")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')

if [ "$HTTP_CODE" -eq 200 ]; then
    USER_ID=$(echo "$BODY" | grep -o '"id":"[^"]*' | cut -d'"' -f4)
    USER_EMAIL=$(echo "$BODY" | grep -o '"email":"[^"]*' | cut -d'"' -f4)
    echo -e "${GREEN}✓${NC} Get user with API Key successful (Status: $HTTP_CODE)"
    echo "  User ID: $USER_ID"
    echo "  User Email: $USER_EMAIL"
else
    echo -e "${RED}✗${NC} Get user with API Key failed (Expected: 200, Got: $HTTP_CODE)"
    echo "  Response: $BODY"
    TEST_RESULT=1
fi

# テスト10: 内部サービス用API Key認証（無効なAPI Key）
echo ""
echo "Test 10: Get user with invalid API Key"
RESPONSE=$(curl -s -w "\n%{http_code}" -X GET "$API_URL/users/28151" \
    -H "X-API-Key: invalid_api_key")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
print_test_result "Get user with invalid API Key" "$HTTP_CODE" 401

echo ""
echo "=========================================="
if [ $TEST_RESULT -eq 0 ]; then
    echo -e "${GREEN}All tests passed!${NC}"
else
    echo -e "${RED}Some tests failed!${NC}"
fi
echo "=========================================="

# サービスを停止するかどうか確認
read -p "Do you want to stop the services? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "Stopping services..."
    docker-compose down
fi

exit $TEST_RESULT
