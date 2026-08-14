# Kubernetes マニフェスト

User ServiceをKubernetesにデプロイするためのマニフェストです。GitHub ActionsのCI/CD（`.github/workflows/deploy.yml`）は既存のDeploymentへ `kubectl set image` するだけなので、初回のみここでDeployment/Serviceを作成してください。

## ファイル構成

- `namespace.yaml` - `gameday-workflow` 名前空間の定義（他サービスと共有）
- `deployment.yaml` - Deployment（1レプリカ）。**GameDay第0章**用に`pod-role: primary`ラベルを持ち、CPU limitを`100m`まで絞った上でコンテナ内に常時CPUを使い切るバックグラウンド処理（`USER_POD_ROLE=primary`で有効化）を仕込んでいる。Serviceは常にこのPodにだけルーティングする
- `standby-deployment.yaml` - もう1台のDeployment（`pod-role: standby`）。同じイメージだがCPUは平常で負荷処理も無効。Serviceからは選ばれず、トラフィックを受けない見せかけのPod
- `service.yaml` - ClusterIP Service。selectorに`pod-role: primary`を含むため、常に上記`deployment.yaml`側のPodだけがエンドポイントになる
- `secret.yaml.example` - Secretの例（実際のSecretは別途作成）

GameDay第0章の詳細（ログインフォームで「問題のPod名」を入力させる仕組みなど）は `docs/gameday-scenario-design.md` を参照。

## 初回デプロイ手順

```bash
# 名前空間の作成（他サービスで既に作成済みならスキップ可）
kubectl apply -f namespace.yaml

# Secretの作成（secret.yaml.exampleを参考に値を差し替えてから作成）
kubectl create secret generic gameday-workflow-user-secrets \
  --from-literal=connection-string='Host=gameday-workflow-db;Port=5432;Database=gameday_workflow_user;Username=gameday_user;Password=gameday_password' \
  --from-literal=jwt-secret-key='YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!' \
  --from-literal=internal-api-key='InternalServiceApiKeyForGameDayWorkflow2024!' \
  --namespace=gameday-workflow

# Deployment / Serviceの作成（primary・standby両方を作成する）
kubectl apply -f deployment.yaml
kubectl apply -f standby-deployment.yaml
kubectl apply -f service.yaml
```

以降のデプロイはCIの `kubectl set image` 相当の処理（`deploy.yml`が両Deploymentのイメージを更新）によって更新されます。

## GameDay第0章: Podが不安定になった場合の緊急停止

`deployment.yaml`側（primary）のCPU負荷が原因で演習が進行できなくなった場合、再デプロイ不要で即座に止められます。

```bash
kubectl set env deployment/gameday-workflow-user CPU_SATURATION_ENABLED=false -n gameday-workflow
```

元に戻す場合は`CPU_SATURATION_ENABLED=true`を再度設定してください（Podが再起動します）。
