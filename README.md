# Document Q&A Assistant

Microsoft Foundry Agent Service を使用した、文書ファイルからの質問抽出と回答生成を行うBlazor Serverアプリケーションです。

## ?? 機能

- ? Word (.docx) / テキスト (.txt) ファイルのアップロード
- ? ファイルからの質問内容抽出
- ? Foundry Agent Service による AI回答生成
- ? リアルタイムな応答表示
- ? モダンで洗練されたUI

## ??? 技術スタック

- **.NET 8** - Blazor Server
- **Microsoft Foundry Agent Service** - AI エージェント
- **Azure.AI.Projects** - Azure AI SDK
- **DocumentFormat.OpenXml** - Word ファイル処理
- **Bootstrap 5** + **Bootstrap Icons** - UI

## ?? 前提条件

- .NET 8 SDK
- Azure CLI
- Microsoft Foundry プロジェクトとエージェント

## ?? セットアップ

### 1. リポジトリをクローン

```bash
git clone <your-repository-url>
cd ResponseAgent
```

### 2. 設定ファイルを作成

`ResponseAgent/appsettings.json.template` をコピーして `appsettings.json` を作成：

```bash
cd ResponseAgent
cp appsettings.json.template appsettings.json
```

### 3. appsettings.json を編集

```json
{
  "FoundryAgent": {
    "Endpoint": "https://YOUR-PROJECT.services.ai.azure.com/api/projects/proj-default",
    "AgentId": "YOUR-AGENT-NAME",
    "ModelDeploymentName": "gpt-4o"
  }
}
```

**取得方法:**
- **Endpoint**: Azure AI Foundry Portal > プロジェクト > Overview > Project connection string
- **AgentId**: Foundry Portal で作成したエージェント名

### 4. Azure CLI でログイン

```bash
az login
```

適切なサブスクリプションを選択してください。

### 5. アプリケーションを実行

```bash
cd ResponseAgent
dotnet run
```

ブラウザで `https://localhost:5001` にアクセス。

## ?? 使い方

1. **ファイルを選択**  
   質問が記載された Word (.docx) または テキスト (.txt) ファイルをアップロード

2. **自動処理**  
   - ファイルから質問内容を自動抽出
   - AI エージェントが回答を生成

3. **結果を確認**  
   - 抽出された質問内容を確認
   - AI生成された回答をレビュー
   - クリップボードにコピー可能

## ?? セキュリティ

?? **重要**: `appsettings.json` は `.gitignore` に含まれています。

- 本番環境では **Azure Key Vault** または **環境変数** を使用してください
- エンドポイントやエージェント情報を公開リポジトリにコミットしないでください

### 環境変数を使用する場合

`Program.cs` で以下のように変更：

```csharp
builder.Configuration.AddEnvironmentVariables();
```

環境変数で設定：

```bash
export FoundryAgent__Endpoint="https://..."
export FoundryAgent__AgentId="your-agent-name"
```

## ?? プロジェクト構造

```
ResponseAgent/
├── Components/
│   ├── Layout/
│   │   └── MainLayout.razor       # レイアウト + フッター
│   ├── Pages/
│   │   └── Home.razor             # メインページ
│   └── App.razor                  # アプリケーションルート
├── Services/
│   ├── AgentService.cs            # Foundry Agent 連携
│   └── FileProcessingService.cs   # ファイル処理
├── wwwroot/
│   └── app.css                    # カスタムスタイル
├── appsettings.json.template      # 設定テンプレート
├── appsettings.json               # (Git非管理) 実際の設定
└── Program.cs                     # エントリポイント
```

## ?? UI スクリーンショット

- グラデーション背景
- カードベースのモダンデザイン
- レスポンシブ対応
- Bootstrap Icons

## ?? コントリビューション

Pull Requestを歓迎します！

## ?? ライセンス

MIT License

## ?? サポート

質問や問題がある場合は、Issueを作成してください。

---

**Powered by Microsoft Foundry Agent Service**
