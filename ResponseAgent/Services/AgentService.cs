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
            // AIProjectClient を作成 (App Service ではマネージドIDを使用)
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

            // 応答テキストを取得
            var responseText = response.GetOutputText();

            _logger.LogInformation("エージェントから応答を受信しました");

            return responseText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AIエージェント実行中にエラーが発生しました");
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
            return "エラー: Review Agent 名が設定されていません。appsettings.json の ReviewAgentId を確認してください。";
        }

        try
        {
            // AIProjectClient を作成 (App Service ではマネージドIDを使用)
            var credential = new DefaultAzureCredential();
            var projectClient = new AIProjectClient(new Uri(endpoint), credential);

            // 会話を作成
            var conversationResult = projectClient.OpenAI.Conversations.CreateProjectConversation();
            var conversation = conversationResult.Value;
            
            _logger.LogInformation("レビュー用会話を作成しました: {ConversationId}", conversation.Id);

            // ProjectResponsesClient を取得（レビューエージェント用）
            var responsesClient = projectClient.OpenAI.GetProjectResponsesClientForAgent(
                defaultAgent: reviewAgentName,
                defaultConversationId: conversation.Id);

            _logger.LogInformation("レビューエージェント '{ReviewAgentName}' にメッセージを送信中...", reviewAgentName);

            // レビュー依頼メッセージを作成
            var reviewRequest = $"以下の答弁案をレビューしてください:\n\n{responseText}";

            // エージェントにメッセージを送信
            var responseResult = await Task.Run(() => responsesClient.CreateResponse(reviewRequest), cancellationToken);
            var response = responseResult.Value;

            // 応答テキストを取得
            var reviewText = response.GetOutputText();

            _logger.LogInformation("レビューエージェントから応答を受信しました");

            return reviewText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "レビューエージェント実行中にエラーが発生しました");
            return $"エラー: {ex.Message}\n\n設定を確認してください:\n- エンドポイント: {endpoint}\n- レビューエージェント名: {reviewAgentName}";
        }
    }
}








