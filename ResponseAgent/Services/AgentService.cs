using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Identity;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace ResponseAgent.Services;

public interface IAgentService
{
    Task<string> GenerateResponseAsync(string questionText, CancellationToken cancellationToken = default);
    Task<string> ReviewResponseAsync(string responseText, CancellationToken cancellationToken = default);
    Task<string> RewriteResponseAsync(string responseText, CancellationToken cancellationToken = default);
    Task<string> ChatAsync(string userMessage, string context, CancellationToken cancellationToken = default);
}

public class AgentService : IAgentService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentService> _logger;

    public AgentService(IConfiguration configuration, ILogger<AgentService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateResponseAsync(
        string questionText,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["FoundryAgent:Endpoint"];
        var agentName = _configuration["FoundryAgent:AgentId"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("Foundry Agent エンドポイントが設定されていません");
            return "エラー: Foundry Agent エンドポイントが設定されていません。appsettings.json を確認してください。";
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            _logger.LogError("Agent 名が設定されていません");
            return "エラー: Agent 名が設定されていません。appsettings.json を確認してください。";
        }

        try
        {
            // AIProjectClient を作成 (App Service ではマネージドID を使用)
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);

            // 会話を作成
            var conversationResult = projectClient.OpenAI.Conversations.CreateProjectConversation();
            var conversation = conversationResult.Value;
            
            _logger.LogInformation("会話を作成しました: {ConversationId}", conversation.Id);

            // ProjectResponsesClient を取得
            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: agentName,
                defaultConversationId: conversation.Id);

            _logger.LogInformation("エージェント '{AgentName}' にメッセージを送信中...", agentName);

            // エージェントにメッセージを送信
            var responseResult = await Task.Run(() => responsesClient.CreateResponse(questionText), cancellationToken);
            var response = responseResult.Value;

            // テキスト内容を取得
            var responseText = response.GetOutputText();

            _logger.LogInformation("エージェントから応答を受け取りました");

            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI エージェント処理中にエラーが発生しました");
            return $"エラー: {ex.Message}\n\n設定を確認してください:\n- エンドポイント: {endpoint}\n- エージェント名: {agentName}";
        }
    }

    public async Task<string> ReviewResponseAsync(
        string responseText,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["FoundryAgent:Endpoint"];
        var reviewAgentName = _configuration["FoundryAgent:ReviewAgentId"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("Foundry Agent エンドポイントが設定されていません");
            return "エラー: Foundry Agent エンドポイントが設定されていません。appsettings.json を確認してください。";
        }

        if (string.IsNullOrWhiteSpace(reviewAgentName))
        {
            _logger.LogError("Review Agent 名が設定されていません");
            return "エラー: Review Agent 名が設定されていません。appsettings.json を確認してください。";
        }

        try
        {
            // AIProjectClient を作成 (App Service ではマネージドID を使用)
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);

            // 会話を作成
            var conversationResult = projectClient.OpenAI.Conversations.CreateProjectConversation();
            var conversation = conversationResult.Value;
            
            _logger.LogInformation("レビュー用の会話を作成しました: {ConversationId}", conversation.Id);

            // ProjectResponsesClient を取得 (レビュー エージェント用)
            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: reviewAgentName,
                defaultConversationId: conversation.Id);

            _logger.LogInformation("レビュー エージェント '{ReviewAgentName}' にメッセージを送信中...", reviewAgentName);

            // レビュー依頼メッセージを作成
            var reviewRequest = $"以下のテキストをレビュー・改善してください。改善箇所を指摘してください:\n\n{responseText}";

            // エージェントにメッセージを送信
            var responseResult = await Task.Run(() => responsesClient.CreateResponse(reviewRequest), cancellationToken);
            var response = responseResult.Value;

            // テキスト内容を取得
            var reviewText = response.GetOutputText();

            _logger.LogInformation("レビュー エージェントから応答を受け取りました");

            return reviewText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "レビュー エージェント処理中にエラーが発生しました");
            return $"エラー: {ex.Message}\n\n設定を確認してください:\n- エンドポイント: {endpoint}\n- レビュー エージェント名: {reviewAgentName}";
        }
    }

    public async Task<string> RewriteResponseAsync(
        string responseText,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["FoundryAgent:Endpoint"];
        var rewriteAgentName = _configuration["FoundryAgent:RewriteAgentId"] ?? "Answer-Rewrite";

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("Foundry Agent エンドポイントが設定されていません");
            return $"エラー: Foundry Agent エンドポイントが設定されていません。appsettings.json を確認してください。";
        }

        if (string.IsNullOrWhiteSpace(rewriteAgentName))
        {
            _logger.LogError("Rewrite Agent 名が設定されていません");
            return $"エラー: Rewrite Agent 名が設定されていません。appsettings.json を確認してください。";
        }

        try
        {
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);

            // 会話を作成
            var conversationResult = projectClient.OpenAI.Conversations.CreateProjectConversation();
            var conversation = conversationResult.Value;
            
            _logger.LogInformation("修正用の会話を作成しました: {ConversationId}", conversation.Id);

            // ProjectResponsesClient を取得 (修正エージェント用)
            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: rewriteAgentName,
                defaultConversationId: conversation.Id);

            _logger.LogInformation("修正エージェント '{RewriteAgentName}' にメッセージを送信中...", rewriteAgentName);

            // 修正リクエストメッセージを作成
            // var rewriteRequest = $"以下の AI 生成テキストを改善・修正してください。修正内容は元のテキストとの差分を含めて提示してください:\n\n{responseText}";
            var rewriteRequest = $"以下の AI 生成テキストを改善・修正してください。:\n\n{responseText}";

            // エージェントにメッセージを送信
            var responseResult = await Task.Run(() => responsesClient.CreateResponse(rewriteRequest), cancellationToken);
            var response = responseResult.Value;

            // 修正されたテキストを取得
            var rewrittenText = response.GetOutputText();

            _logger.LogInformation("修正エージェントから応答を受け取りました");

            return rewrittenText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修正エージェント処理中にエラーが発生しました");
            return $"エラー: {ex.Message}\n\n設定を確認してください:\n- エンドポイント: {endpoint}\n- 修正エージェント名: {rewriteAgentName}";
        }
    }

    public async Task<string> ChatAsync(
        string userMessage,
        string context,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["FoundryAgent:Endpoint"];
        var chatAgentName = _configuration["FoundryAgent:ChatAgentId"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogError("Foundry Agent エンドポイントが設定されていません");
            return "エラー: Foundry Agent エンドポイントが設定されていません。";
        }

        if (string.IsNullOrWhiteSpace(chatAgentName))
        {
            _logger.LogError("Chat Agent 名が設定されていません");
            return "エラー: Chat Agent 名が設定されていません。appsettings.json を確認してください。";
        }

        try
        {
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);

            var conversationResult = projectClient.OpenAI.Conversations.CreateProjectConversation();
            var conversation = conversationResult.Value;
            
            _logger.LogInformation("チャット用の会話を作成しました: {ConversationId}", conversation.Id);

            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: chatAgentName,
                defaultConversationId: conversation.Id);

            _logger.LogInformation("チャット エージェント '{ChatAgentName}' にメッセージを送信中...", chatAgentName);

            // コンテキストを含めたメッセージを作成
            var messageWithContext = string.IsNullOrWhiteSpace(context)
                ? userMessage
                : $"【既知の背景】\n{context}\n\n【ユーザーの質問】\n{userMessage}";

            var responseResult = await Task.Run(() => responsesClient.CreateResponse(messageWithContext), cancellationToken);
            var response = responseResult.Value;

            var chatResponse = response.GetOutputText();

            _logger.LogInformation("チャット エージェントから応答を受け取りました");

            return chatResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "チャット エージェント処理中にエラーが発生しました");
            return $"エラー: {ex.Message}";
        }
    }
}








