# User Service (.NET) プロジェクト設計

## リポジトリ名
`gameday-workflow-user-service`

## 技術スタック
- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- JWT認証

## プロジェクト構成

```
gameday-workflow-user-service/
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── deploy.yml
├── src/
│   ├── UserService.Api/            # Web APIプロジェクト
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   └── UsersController.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   ├── UserService.Application/   # アプリケーション層
│   │   ├── Services/
│   │   │   ├── AuthService.cs
│   │   │   └── UserService.cs
│   │   └── DTOs/
│   ├── UserService.Domain/        # ドメイン層
│   │   ├── Entities/
│   │   └── ValueObjects/
│   └── UserService.Infrastructure/ # インフラ層
│       ├── Data/
│       │   ├── ApplicationDbContext.cs
│       │   └── Repositories/
│       └── Services/
├── tests/
│   ├── UserService.Api.Tests/
│   ├── UserService.Application.Tests/
│   └── UserService.Infrastructure.Tests/
├── docker-compose.yml
├── Dockerfile
├── .dockerignore
├── .gitignore
├── UserService.sln
└── README.md
```

## ローカル開発環境の設定

### Docker Composeによるデータベース構築

PostgreSQLをdocker-composeで起動し、単体で動作可能な環境を構築します。

- `docker-compose.yml`を作成し、PostgreSQL 16のコンテナを定義
- データベース名: `gameday_workflow_user`
- ユーザー名: `gameday_user`
- ポート: `5432`
- データ永続化のためボリュームをマウント
- ヘルスチェックを設定

### データベース接続設定

`appsettings.Development.json`に接続文字列を設定します。

- ホスト: `localhost`
- ポート: `5432`
- データベース名: `gameday_workflow_user`
- ユーザー名: `gameday_user`
- パスワード: `gameday_password`

### データベース初期化とシードデータ

アプリケーション起動時にEF Core Migrationsを実行し、初期ユーザーデータを投入します。

- `DbInitializer`クラスを作成し、`InitializeAsync`メソッドで初期化処理を実装
- マイグレーション実行後、既にデータが存在する場合はスキップ
- 各ロールごとに50名のユーザーを自動生成
  - 本部長 (director): ID 1051-1100
  - 経理 (accounting): ID 16051-16100
  - 上長 (manager): ID 21051-21100
  - 開発エンジニア (engineer): ID 28151-28200
- 各ユーザーのデフォルトパスワードは `password`（BCryptでハッシュ化）
- 各ロールの最初のユーザー（ID: 1051, 16051, 21051, 28151）は代表メールアドレスを使用

### アプリケーション起動時の初期化

`Program.cs`でアプリケーション起動時に`DbInitializer.InitializeAsync`を呼び出し、データベースを初期化します。

## テスト用スタブ実装

### インメモリデータベースを使用

単体テストでは、インメモリデータベースを使用します。

- `TestStartup`クラスでテスト用のサービス設定を行う
- `ApplicationDbContext`をインメモリデータベースに設定
- 必要に応じてモックサービスを登録

### モックサービスの実装

テスト用のモックサービスを実装します。

- `MockUserService`クラスで`IUserService`インターフェースを実装
- テスト用のユーザーデータを保持
- 必要最小限のメソッドを実装

## 単体テスト構成

xUnitを使用して単体テストを実装します。

- `AuthServiceTests`クラスで認証サービスのテストを実装
- Moqを使用してリポジトリをモック化
- Arrange-Act-Assertパターンでテストを記述

## GitHub Actions設定

### CI/CDパイプライン (.github/workflows/ci.yml)

mainブランチとdevelopブランチへのpush、およびプルリクエスト時にCIを実行します。

- .NET 8.0のセットアップ
- 依存関係の復元
- ビルド
- テストの実行

### デプロイワークフロー (.github/workflows/deploy.yml)

mainブランチへのpush時にEKSへデプロイします。

- AWS認証情報の設定
- Amazon ECRへのログイン
- Dockerイメージのビルド、タグ付け、プッシュ
- EKSクラスターへのkubeconfig更新
- Kubernetesデプロイメントの更新とロールアウト状態の確認

## Dockerfile

マルチステージビルドを使用してDockerイメージを構築します。

- ベースイメージ: `mcr.microsoft.com/dotnet/aspnet:8.0`
- ビルドイメージ: `mcr.microsoft.com/dotnet/sdk:8.0`
- ポート80を公開
- リリースモードでビルド・公開

## ユーザーデータ設計

### ロールとユーザーID範囲

各ロールごとに50名のユーザーを用意します：

| ロール | ユーザーID範囲 | 人数 | 説明 |
|--------|---------------|------|------|
| director | 1051-1100 | 50名 | 本部長 |
| accounting | 16051-16100 | 50名 | 経理 |
| manager | 21051-21100 | 50名 | 上長 |
| engineer | 28151-28200 | 50名 | 開発エンジニア |

### テスト用ログイン情報

以下のメールアドレスでログインすると、それぞれの役割でログインできます（全ユーザーのデフォルトパスワードは `password`）：

- `director@example.com` → 本部長（ID: 1051）
- `accounting@example.com` → 経理（ID: 16051）
- `manager@example.com` → 上長（ID: 21051）
- `engineer@example.com` → 開発エンジニア（ID: 28151）

### ローカル開発環境の起動方法

1. PostgreSQLを起動: `docker-compose up -d`
2. アプリケーションを起動: `cd src/UserService.Api && dotnet run`
3. データベースの状態確認: `docker-compose exec postgres psql -U gameday_user -d gameday_workflow_user -c "SELECT COUNT(*) FROM Users;"`

