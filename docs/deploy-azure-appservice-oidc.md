# Azure App Service へのデプロイ設定（OIDC認証）

このドキュメントでは、GitHub Actions から Azure App Service へ OIDC（OpenID Connect）認証を使用してデプロイする方法を説明します。

## 概要

- **デプロイ先**: Azure App Service `app-20260115` の `staging` スロット
- **URL**: https://app-20260115-staging.azurewebsites.net/
- **認証方式**: GitHub Actions OIDC（Publish Profile は使用しない）
- **トリガー**: `main` ブランチへの push

## Azure 側の設定

### 1. Azure App Service の準備

Azure App Service `app-20260115` とその `staging` スロットが作成済みであることを確認してください。

### 2. Managed Identity の作成または使用

デプロイに使用する Azure AD アプリケーション（Service Principal）を準備します。

#### オプション A: 既存の Service Principal を使用

既存の Service Principal がある場合は、その情報を使用します。

#### オプション B: 新しい Service Principal を作成

```bash
# Azure CLI でログイン
az login

# Service Principal を作成（App Service の Contributor ロールを付与）
az ad sp create-for-rbac --name "github-actions-blazor-qa" \
  --role contributor \
  --scopes /subscriptions/{SUBSCRIPTION_ID}/resourceGroups/{RESOURCE_GROUP}/providers/Microsoft.Web/sites/app-20260115 \
  --sdk-auth
```

出力される JSON から以下の情報を記録します：
- `clientId`
- `tenantId`
- `subscriptionId`

### 3. Federated Credential の作成

GitHub Actions からの OIDC 認証を許可するため、Federated Credential を作成します。

#### Azure Portal での設定

1. Azure Portal にアクセス
2. **Azure Active Directory** > **App registrations** に移動
3. 作成した Service Principal（アプリケーション）を選択
4. **Certificates & secrets** > **Federated credentials** タブを開く
5. **Add credential** をクリック
6. 以下の情報を入力：
   - **Federated credential scenario**: GitHub Actions deploying Azure resources
   - **Organization**: `kanazawazawa`
   - **Repository**: `blazor-document-qa-assistant`
   - **Entity type**: Branch
   - **GitHub branch name**: `main`
   - **Name**: `github-actions-main-branch`（任意の名前）

#### Azure CLI での設定

```bash
# アプリケーション（Service Principal）の Object ID を取得
APP_OBJECT_ID=$(az ad app list --display-name "github-actions-blazor-qa" --query "[0].id" -o tsv)

# Federated Credential を作成
az ad app federated-credential create \
  --id $APP_OBJECT_ID \
  --parameters '{
    "name": "github-actions-main-branch",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:kanazawazawa/blazor-document-qa-assistant:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

**重要**: `subject` は正確に `repo:kanazawazawa/blazor-document-qa-assistant:ref:refs/heads/main` である必要があります。

### 4. App Service へのアクセス権限を付与

Service Principal に App Service への適切なアクセス権限を付与します。

```bash
# リソースグループ名を設定
RESOURCE_GROUP="your-resource-group-name"
SUBSCRIPTION_ID="your-subscription-id"
CLIENT_ID="your-client-id"

# Contributor ロールを付与（App Service レベル）
az role assignment create \
  --assignee $CLIENT_ID \
  --role "Contributor" \
  --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/app-20260115
```

または、Deployment Slot レベルで権限を付与：

```bash
# staging スロットに対して権限を付与
az role assignment create \
  --assignee $CLIENT_ID \
  --role "Website Contributor" \
  --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/app-20260115/slots/staging
```

## GitHub 側の設定

### GitHub Secrets の設定

GitHub リポジトリに以下の Secrets を設定します。

1. リポジトリの **Settings** > **Secrets and variables** > **Actions** に移動
2. **New repository secret** をクリック
3. 以下の 3 つの Secrets を作成：

| Secret 名               | 値                                    | 説明                        |
|------------------------|---------------------------------------|---------------------------|
| `AZURE_CLIENT_ID`      | Service Principal の Client ID        | Azure AD アプリケーションの ID |
| `AZURE_TENANT_ID`      | Azure AD のテナント ID                | Azure AD テナントの ID       |
| `AZURE_SUBSCRIPTION_ID`| Azure サブスクリプション ID             | デプロイ先のサブスクリプション   |

### ワークフローファイルの確認

`.github/workflows/deploy-staging.yml` が正しく設定されていることを確認します。

主要な設定項目：
- **トリガー**: `main` ブランチへの push
- **Permissions**: `id-token: write` と `contents: read`
- **環境変数**: 
  - `AZURE_WEBAPP_NAME: app-20260115`
  - `AZURE_WEBAPP_SLOT: staging`
  - `PROJECT_PATH: ResponseAgent/ResponseAgent.csproj`
  - `DOTNET_VERSION: 8.0.x`

## デプロイの実行

### 自動デプロイ

`main` ブランチに push すると、自動的にデプロイワークフローが実行されます。

```bash
git push origin main
```

### デプロイの確認

1. GitHub リポジトリの **Actions** タブで実行状況を確認
2. ワークフローが完了したら、以下の URL にアクセスして動作確認：
   ```
   https://app-20260115-staging.azurewebsites.net/
   ```

## トラブルシューティング

### エラー: "Failed to get federated token"

- Federated Credential の `subject` が正しいことを確認
- GitHub Actions の permissions が `id-token: write` に設定されていることを確認

### エラー: "Authorization failed"

- Service Principal が App Service へのアクセス権限を持っていることを確認
- GitHub Secrets が正しく設定されていることを確認

### エラー: "Resource not found"

- `AZURE_WEBAPP_NAME` と `AZURE_WEBAPP_SLOT` が正しいことを確認
- App Service と Deployment Slot が存在することを確認

### デプロイは成功するがアプリが動作しない

- Azure Portal で App Service のログを確認
- `appsettings.json` の設定が環境変数または Azure App Configuration で提供されていることを確認
- Blazor Server アプリの場合、WebSocket が有効になっていることを確認

## ベストプラクティス

1. **環境変数の管理**: 機密情報は Azure Key Vault または App Service の Application Settings で管理
2. **スロット戦略**: staging で検証後、production スロットへスワップ
3. **モニタリング**: Application Insights を有効化してアプリケーションの健全性を監視
4. **ロールバック**: デプロイに問題があれば、Azure Portal からスロットスワップで前のバージョンに戻す

## 参考リンク

- [GitHub Actions で Azure にログインする (OIDC)](https://learn.microsoft.com/ja-jp/azure/developer/github/connect-from-azure)
- [Azure App Service への継続的デプロイ](https://learn.microsoft.com/ja-jp/azure/app-service/deploy-continuous-deployment)
- [Azure Web Apps Deploy Action](https://github.com/Azure/webapps-deploy)
