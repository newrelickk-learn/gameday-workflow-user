# User Service

.NET 8を使用したASP.NET Core Web APIプロジェクトです。

## 技術スタック

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- JWT認証

## プロジェクト構成

```
gameday-workflow-user-service/
├── src/
│   ├── UserService.Api/            # Web APIプロジェクト
│   ├── UserService.Application/   # アプリケーション層
│   ├── UserService.Domain/        # ドメイン層
│   └── UserService.Infrastructure/ # インフラ層
├── tests/                          # テストプロジェクト
├── docker-compose.yml              # PostgreSQL用Docker Compose
├── Dockerfile                      # Dockerイメージ定義
└── Makefile                        # ビルド・実行用Makefile
```

## セットアップ

### 前提条件

- Docker & Docker Compose
- Make

**注意**: .NET SDKは不要です。すべての操作はdocker-compose経由で実行されます。

### ローカル開発環境の起動

すべての操作はdocker-compose経由で実行されます。.NET SDKのインストールは不要です。

1. 依存関係を復元:
```bash
make restore
```

2. プロジェクトをビルド:
```bash
make build
```

3. すべてのサービスを起動（PostgreSQL + API）:
```bash
make docker-compose-up
```

または、個別に実行する場合:

```bash
# PostgreSQLのみ起動
make db-up

# ビルドとDB起動を同時に
make build-local

# 全サービス起動
make run-local
```

## Makefileコマンド

- `make help` - 利用可能なコマンド一覧を表示
- `make build` - .NETソリューションをビルド
- `make run` - APIをローカルで実行（PostgreSQLが起動している必要があります）
- `make test` - すべてのテストを実行
- `make test-coverage` - コードカバレッジを取得
- `make test-coverage-html` - HTMLカバレッジレポートを生成
- `make test-mutation` - Mutation Testingを実行（Stryker.NET）
- `make clean` - ビルド成果物をクリーン
- `make docker-build` - Dockerイメージをビルド（ローカル用ARM64）
- `make docker-build-amd64` - Dockerイメージをビルド（本番用AMD64/x86-64）
- `make docker-run` - Dockerコンテナを実行
- `make db-up` - PostgreSQLデータベースを起動
- `make db-down` - PostgreSQLデータベースを停止
- `make db-reset` - データベースをリセット（ボリュームも削除）
- `make migrate` - EF Coreマイグレーションを実行
- `make restore` - NuGetパッケージを復元
- `make build-local` - ローカルビルド（DB起動、復元、ビルド）
- `make run-local` - ローカル実行（DB起動、API実行）
- `make docker-compose-up` - docker-composeで全サービスを起動
- `make docker-compose-down` - docker-composeで全サービスを停止
- `make docker-compose-logs` - docker-composeのログを表示

## Dockerビルド

### ローカル（Mac ARM64）用
```bash
make docker-build
```

### 本番（x86-64/AMD64）用
```bash
make docker-build-amd64
```

## データベース

### 接続情報

- ホスト: `localhost`
- ポート: `5432`
- データベース名: `gameday_workflow_user`
- ユーザー名: `gameday_user`
- パスワード: `gameday_password`

### 初期データ

アプリケーション起動時に自動的に以下のユーザーが生成されます：

| ロール | ユーザーID範囲 | 人数 | 代表メールアドレス |
|--------|---------------|------|-------------------|
| director | 1051-1100 | 50名 | director@example.com |
| accounting | 16051-16100 | 50名 | accounting@example.com |
| manager | 21051-21100 | 50名 | manager@example.com |
| engineer | 28151-28200 | 50名 | engineer@example.com |

全ユーザーのデフォルトパスワードは `password` です。

## APIエンドポイント

### 認証

- `POST /auth/login` - ログイン（認証不要）

### ユーザー

- `GET /users/{id}` - 特定のユーザーを取得（認証必要）

## テスト用ログイン情報

以下のメールアドレスとパスワード `password` でログインできます：

- `director@example.com` → 本部長（ID: 1051）
- `accounting@example.com` → 経理（ID: 16051）
- `manager@example.com` → 上長（ID: 21051）
- `engineer@example.com` → 開発エンジニア（ID: 28151）

## データベースの状態確認

```bash
docker-compose exec postgres psql -U gameday_user -d gameday_workflow_user -c "SELECT COUNT(*) FROM Users;"
```

## テストとカバレッジ

すべてのテストコマンドはdocker-compose経由で実行されます。

### テストの実行

```bash
# すべてのテストを実行（docker-compose経由）
make test

# コードカバレッジを取得（docker-compose経由）
make test-coverage

# HTMLカバレッジレポートを生成（docker-compose経由）
make test-coverage-html
# レポートは ./coverage/html/index.html で確認できます
```

### Mutation Testing (MCC)

Mutation Testing（変異テスト）を実行して、テストの品質を評価できます：

```bash
# Mutation Testingを実行（docker-compose経由）
make test-mutation
```

Stryker.NETを使用してMutation Testingを実行します。結果は `src/UserService.Api/StrykerOutput/` に生成されます。

### Docker Composeでの実行

```bash
# 全サービスを起動（PostgreSQL + API）
make docker-compose-up

# APIは http://localhost:8001 で利用可能
# ヘルスチェック: http://localhost:8001/health

# ログを確認
make docker-compose-logs

# サービスを停止
make docker-compose-down
```

## 開発

### マイグレーションの作成

```bash
# docker-compose経由でマイグレーションを作成
docker-compose run --rm migrate dotnet ef migrations add MigrationName --project src/UserService.Api
```

### マイグレーションの適用

```bash
# docker-compose経由でマイグレーションを適用
make migrate
```

