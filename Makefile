.PHONY: help build restore test test-coverage test-coverage-html test-mutation clean migrate docker-build docker-build-amd64 docker-compose-up docker-compose-down docker-compose-logs db-up db-down db-reset build-local run-local

# Variables
DOCKER_IMAGE_NAME := gameday-workflow-user-service
DOCKER_TAG := latest
LOCAL_PLATFORM := linux/arm64
PROD_PLATFORM := linux/amd64
COMPOSE := docker-compose

help: ## Show this help message
	@echo 'Usage: make [target]'
	@echo ''
	@echo 'Available targets:'
	@awk 'BEGIN {FS = ":.*?## "} /^[a-zA-Z_-]+:.*?## / {printf "  %-20s %s\n", $$1, $$2}' $(MAKEFILE_LIST)

build: ## Build the .NET solution (via docker-compose)
	$(COMPOSE) run --rm builder

restore: ## Restore NuGet packages (via docker-compose)
	$(COMPOSE) run --rm restore

test: ## Run all tests (via shell script)
	./scripts/test-api.sh

test-coverage: ## Run tests with code coverage (via shell script)
	./scripts/test-api-coverage.sh

test-coverage-html: ## Generate HTML coverage report (via shell script)
	./scripts/test-api-coverage-html.sh

test-mutation: ## Run mutation testing with Stryker.NET (via shell script)
	./scripts/test-api-mutation.sh

clean: ## Clean build artifacts (via docker-compose)
	$(COMPOSE) run --rm clean

migrate: ## Run EF Core migrations (via docker-compose)
	$(COMPOSE) run --rm migrate

migration-create: ## Create a new EF Core migration (usage: make migration-create MIGRATION_NAME=MigrationName)
	./scripts/create-migration.sh $(MIGRATION_NAME)

docker-build: ## Build Docker image for local (ARM64/Mac)
	DOCKER_DEFAULT_PLATFORM=$(LOCAL_PLATFORM) $(COMPOSE) build workflow-user

docker-build-amd64: ## Build Docker image for production (AMD64/x86-64)
	DOCKER_DEFAULT_PLATFORM=$(PROD_PLATFORM) $(COMPOSE) build workflow-user

docker-compose-up: ## Start all services with docker-compose
	$(COMPOSE) up -d
	@echo "Waiting for services to be ready..."
	@sleep 10
	@echo "API is available at http://localhost:8001"

docker-compose-down: ## Stop all docker-compose services
	$(COMPOSE) down

docker-compose-logs: ## Show docker-compose logs
	$(COMPOSE) logs -f

db-up: ## Start PostgreSQL database (注意: gameday-workflow-db ディレクトリで実行してください)
	@echo "データベースは gameday-workflow-db サービスで管理されています。"
	@echo "データベースを起動するには、以下のコマンドを実行してください:"
	@echo "  cd ../gameday-workflow-db && make up"

db-down: ## Stop PostgreSQL database (注意: gameday-workflow-db ディレクトリで実行してください)
	@echo "データベースは gameday-workflow-db サービスで管理されています。"
	@echo "データベースを停止するには、以下のコマンドを実行してください:"
	@echo "  cd ../gameday-workflow-db && make down"

db-reset: ## Reset database (注意: gameday-workflow-db ディレクトリで実行してください)
	@echo "データベースは gameday-workflow-db サービスで管理されています。"
	@echo "データベースをリセットするには、以下のコマンドを実行してください:"
	@echo "  cd ../gameday-workflow-db && make reset"

build-local: db-up restore build ## Build locally (start DB, restore packages, build)

run-local: docker-compose-up ## Run locally (start all services with docker-compose)
